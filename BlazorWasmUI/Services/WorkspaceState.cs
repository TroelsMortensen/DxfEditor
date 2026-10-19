using System.Text.RegularExpressions;
using BlazorWasmUI.Models;

namespace BlazorWasmUI.Services;

public sealed class WorkspaceState
{
    private const string FallbackStrokeHex = "#000000";

    private readonly List<PlacedPart> _parts = [];
    private readonly List<LayerDefinition> _layers = [];
    private readonly HashSet<Guid> _selectedPartIds = [];
    private readonly HashSet<Guid> _selectedEntityIds = [];
    private int _nextLayerNumber = 1;

    public IReadOnlyList<PlacedPart> Parts => _parts;
    public IReadOnlyList<LayerDefinition> Layers => _layers;
    public IReadOnlyCollection<Guid> SelectedPartIds => _selectedPartIds;
    public IReadOnlyCollection<Guid> SelectedEntityIds => _selectedEntityIds;

    public bool HasSelection => _selectedPartIds.Count > 0 || _selectedEntityIds.Count > 0;

    public double PanX { get; private set; }
    public double PanY { get; private set; }
    public double Zoom { get; private set; } = 1;
    public double CssWidth { get; private set; }
    public double CssHeight { get; private set; }

    private const int StatusClearDelayMs = 5000;

    public string? StatusMessage { get; private set; }

    private CancellationTokenSource? _statusClearCts;

    public event Action? Changed;
    public event Action? CenterViewRequested;

    public void SetStatus(string? message)
    {
        _statusClearCts?.Cancel();
        _statusClearCts?.Dispose();
        _statusClearCts = null;

        StatusMessage = message;
        Notify();

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _statusClearCts = new CancellationTokenSource();
        var token = _statusClearCts.Token;
        _ = ClearStatusAfterDelayAsync(token);
    }

    private async Task ClearStatusAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(StatusClearDelayMs, token);
            StatusMessage = null;
            Notify();
        }
        catch (OperationCanceledException)
        {
            // A newer status replaced this one.
        }
        catch (ObjectDisposedException)
        {
            // Timer was cancelled and disposed.
        }
    }

    public LayerDefinition EnsureLayer(string name, string colorHex)
    {
        var hex = LayerPalette.NormalizeHex(colorHex);
        var existing = _layers.FirstOrDefault(l =>
            string.Equals(l.ColorHex, hex, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var layer = new LayerDefinition
        {
            Name = string.IsNullOrWhiteSpace(name) ? NextAutoLayerName() : name.Trim(),
            ColorHex = hex,
        };
        _layers.Add(layer);
        Notify();
        return layer;
    }

    /// <summary>Merge many layers and raise a single change notification.</summary>
    public IReadOnlyList<LayerDefinition> EnsureLayers(
        IEnumerable<(string Name, string ColorHex)> layers)
    {
        var result = new List<LayerDefinition>();
        var added = false;
        foreach (var (name, colorHex) in layers)
        {
            var hex = LayerPalette.NormalizeHex(colorHex);
            var existing = _layers.FirstOrDefault(l =>
                string.Equals(l.ColorHex, hex, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                result.Add(existing);
                continue;
            }

            var layer = new LayerDefinition
            {
                Name = string.IsNullOrWhiteSpace(name) ? NextAutoLayerName() : name.Trim(),
                ColorHex = hex,
            };
            _layers.Add(layer);
            result.Add(layer);
            added = true;
        }

        if (added)
        {
            Notify();
        }

        return result;
    }

    public LayerDefinition? AddLayerFromPalette(string colorHex)
    {
        var hex = LayerPalette.NormalizeHex(colorHex);
        if (_layers.Any(l => string.Equals(l.ColorHex, hex, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!LayerPalette.Colors.Any(c => string.Equals(c, hex, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var layer = new LayerDefinition
        {
            Name = NextAutoLayerName(),
            ColorHex = hex,
        };
        _layers.Add(layer);
        Notify();
        return layer;
    }

    public IReadOnlyList<PartEntity> GetLayerAssignmentTargets()
    {
        if (_selectedPartIds.Count > 0)
        {
            return _parts
                .Where(p => _selectedPartIds.Contains(p.Id))
                .SelectMany(p => p.Entities)
                .ToList();
        }

        if (_selectedEntityIds.Count == 0)
        {
            return [];
        }

        return _parts
            .SelectMany(p => p.Entities)
            .Where(e => _selectedEntityIds.Contains(e.Id))
            .ToList();
    }

    public void AssignSelectionToLayer(Guid layerId)
    {
        if (!HasSelection || _layers.All(l => l.Id != layerId))
        {
            return;
        }

        var targets = GetLayerAssignmentTargets();
        var changed = false;
        foreach (var entity in targets)
        {
            if (entity.LayerId != layerId)
            {
                entity.LayerId = layerId;
                changed = true;
            }
        }

        if (changed)
        {
            Notify();
        }
    }

    public void AddParts(IEnumerable<PlacedPart> parts, bool spreadEvenly)
    {
        var list = parts.ToList();
        if (list.Count == 0)
        {
            return;
        }

        if (spreadEvenly)
        {
            SpreadParts(list);
        }

        _parts.AddRange(list);
        Notify();
    }

    private string NextAutoLayerName() => $"Layer {_nextLayerNumber++}";

    public IReadOnlyList<PlacedPart> GetEditableParts()
    {
        if (_selectedPartIds.Count > 0)
        {
            return _parts.Where(p => _selectedPartIds.Contains(p.Id)).ToList();
        }

        if (_selectedEntityIds.Count == 0)
        {
            return [];
        }

        return _parts
            .Where(p => p.Entities.Any(e => _selectedEntityIds.Contains(e.Id)))
            .ToList();
    }

    public void ClearSelection()
    {
        if (_selectedPartIds.Count == 0 && _selectedEntityIds.Count == 0)
        {
            return;
        }

        _selectedPartIds.Clear();
        _selectedEntityIds.Clear();
        Notify();
    }

    public void SetPartSelection(IEnumerable<Guid> ids, bool clearExisting = true)
    {
        if (clearExisting)
        {
            _selectedPartIds.Clear();
            _selectedEntityIds.Clear();
        }

        foreach (var id in ids)
        {
            if (_parts.Any(p => p.Id == id))
            {
                _selectedPartIds.Add(id);
            }
        }

        if (_selectedPartIds.Count > 0)
        {
            _selectedEntityIds.Clear();
        }

        Notify();
    }

    public void SetEntitySelection(IEnumerable<Guid> ids, bool clearExisting = true)
    {
        if (clearExisting)
        {
            _selectedPartIds.Clear();
            _selectedEntityIds.Clear();
        }

        var known = _parts.SelectMany(p => p.Entities.Select(e => e.Id)).ToHashSet();
        foreach (var id in ids)
        {
            if (known.Contains(id))
            {
                _selectedEntityIds.Add(id);
            }
        }

        if (_selectedEntityIds.Count > 0)
        {
            _selectedPartIds.Clear();
        }

        Notify();
    }

    public void SetSelection(IEnumerable<Guid> partIds, IEnumerable<Guid> entityIds)
    {
        _selectedPartIds.Clear();
        _selectedEntityIds.Clear();

        var partIdList = partIds.ToList();
        var entityIdList = entityIds.ToList();

        // Prefer part selection when both are sent (mutual exclusion).
        if (partIdList.Count > 0)
        {
            foreach (var id in partIdList)
            {
                if (_parts.Any(p => p.Id == id))
                {
                    _selectedPartIds.Add(id);
                }
            }
        }
        else
        {
            var known = _parts.SelectMany(p => p.Entities.Select(e => e.Id)).ToHashSet();
            foreach (var id in entityIdList)
            {
                if (known.Contains(id))
                {
                    _selectedEntityIds.Add(id);
                }
            }
        }

        Notify();
    }

    public void ToggleSelection(Guid id)
    {
        if (!_parts.Any(p => p.Id == id))
        {
            return;
        }

        _selectedEntityIds.Clear();
        if (!_selectedPartIds.Add(id))
        {
            _selectedPartIds.Remove(id);
        }

        Notify();
    }

    public void SelectOnly(Guid id)
    {
        _selectedPartIds.Clear();
        _selectedEntityIds.Clear();
        if (_parts.Any(p => p.Id == id))
        {
            _selectedPartIds.Add(id);
        }

        Notify();
    }

    public void ApplyTransforms(IEnumerable<PartTransformDto> transforms)
    {
        var map = transforms.ToDictionary(t => t.Id);
        foreach (var part in _parts)
        {
            var key = part.Id.ToString();
            if (!map.TryGetValue(key, out var t))
            {
                continue;
            }

            part.OffsetX = t.OffsetX;
            part.OffsetY = t.OffsetY;
            part.RotationDegrees = t.RotationDegrees;
            part.Mirrored = t.Mirrored;
        }

        Notify();
    }

    public void SetViewport(double panX, double panY, double zoom, double cssWidth, double cssHeight)
    {
        CssWidth = cssWidth;
        CssHeight = cssHeight;

        var clamped = Math.Clamp(zoom, 0.05, 100);
        if (Math.Abs(PanX - panX) < 1e-9 && Math.Abs(PanY - panY) < 1e-9 && Math.Abs(Zoom - clamped) < 1e-12)
        {
            return;
        }

        PanX = panX;
        PanY = panY;
        Zoom = clamped;
        Notify();
    }

    public void SetCanvasSize(double cssWidth, double cssHeight)
    {
        CssWidth = cssWidth;
        CssHeight = cssHeight;
    }

    public Point2 GetViewCenterWorld()
    {
        if (CssWidth <= 0 || CssHeight <= 0 || Zoom <= 0)
        {
            return new Point2(0, 0);
        }

        return new Point2(
            (CssWidth * 0.5 - PanX) / Zoom,
            -((CssHeight * 0.5 - PanY) / Zoom));
    }

    public void RequestCenterViewOnWorkspace()
    {
        if (_parts.Count == 0)
        {
            return;
        }

        CenterViewRequested?.Invoke();
    }

    public void MirrorSelectionHorizontal()
    {
        var selected = GetEditableParts();
        if (selected.Count == 0)
        {
            return;
        }

        var centroid = GetSelectionCentroid(selected);

        foreach (var part in selected)
        {
            part.OffsetX = 2 * centroid.X - part.OffsetX;
            part.Mirrored = !part.Mirrored;
            part.RotationDegrees = -part.RotationDegrees;
        }

        Notify();
    }

    public void MirrorSelectionVertical()
    {
        var selected = GetEditableParts();
        if (selected.Count == 0)
        {
            return;
        }

        var centroid = GetSelectionCentroid(selected);

        foreach (var part in selected)
        {
            part.OffsetY = 2 * centroid.Y - part.OffsetY;
            part.Mirrored = !part.Mirrored;
            part.RotationDegrees = 180 - part.RotationDegrees;
        }

        Notify();
    }

    public int DeleteSelection()
    {
        var selected = GetEditableParts();
        if (selected.Count == 0)
        {
            return 0;
        }

        var removeIds = selected.Select(p => p.Id).ToHashSet();
        var removed = _parts.RemoveAll(p => removeIds.Contains(p.Id));
        _selectedPartIds.Clear();
        _selectedEntityIds.Clear();
        if (removed > 0)
        {
            SetStatus(removed == 1 ? "Deleted 1 part." : $"Deleted {removed} parts.");
        }

        return removed;
    }

    public int DuplicateSelection(double? placeAtWorldX = null, double? placeAtWorldY = null)
    {
        var selected = GetEditableParts();
        if (selected.Count == 0)
        {
            return 0;
        }

        double dx;
        double dy;
        if (placeAtWorldX is { } wx && placeAtWorldY is { } wy)
        {
            var centroid = GetSelectionCentroid(selected);
            dx = wx - centroid.X;
            dy = wy - centroid.Y;
        }
        else
        {
            var zoom = Math.Max(Zoom, 1e-9);
            dx = 50.0 / zoom;
            dy = -50.0 / zoom;
        }

        var takenNames = _parts.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var clones = new List<PlacedPart>(selected.Count);

        foreach (var source in selected)
        {
            var name = NextUniqueCopyName(source.Name, takenNames);
            takenNames.Add(name);

            var entities = source.Entities
                .Select(e => new PartEntity
                {
                    Polyline = e.Polyline.ToList(),
                    LayerId = e.LayerId,
                    SourceColorHex = e.SourceColorHex,
                })
                .ToList();

            clones.Add(new PlacedPart
            {
                Name = name,
                Entities = entities,
                LocalBounds = source.LocalBounds,
                OffsetX = source.OffsetX + dx,
                OffsetY = source.OffsetY + dy,
                RotationDegrees = source.RotationDegrees,
                Mirrored = source.Mirrored,
            });
        }

        _parts.AddRange(clones);
        _selectedPartIds.Clear();
        _selectedEntityIds.Clear();
        foreach (var clone in clones)
        {
            _selectedPartIds.Add(clone.Id);
        }

        SetStatus(clones.Count == 1 ? "Duplicated 1 part." : $"Duplicated {clones.Count} parts.");
        return clones.Count;
    }

    private static string NextUniqueCopyName(string sourceName, HashSet<string> taken)
    {
        var baseName = StripCopySuffix(sourceName);
        for (var n = 1; ; n++)
        {
            var candidate = $"{baseName} ({n})";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string StripCopySuffix(string name)
    {
        var match = Regex.Match(name, @"^(.*) \((\d+)\)$");
        return match.Success ? match.Groups[1].Value : name;
    }

    public SceneDto BuildScene()
    {
        var scene = new SceneDto
        {
            SelectedPartIds = _selectedPartIds.Select(id => id.ToString()).ToList(),
            SelectedEntityIds = _selectedEntityIds.Select(id => id.ToString()).ToList(),
            Viewport = new ViewportDto
            {
                PanX = PanX,
                PanY = PanY,
                Zoom = Zoom,
            },
        };

        foreach (var part in _parts)
        {
            scene.Parts.Add(new ScenePartDto
            {
                Id = part.Id.ToString(),
                Name = part.Name,
                OffsetX = part.OffsetX,
                OffsetY = part.OffsetY,
                RotationDegrees = part.RotationDegrees,
                Mirrored = part.Mirrored,
                LocalMinX = part.LocalBounds.MinX,
                LocalMinY = part.LocalBounds.MinY,
                LocalMaxX = part.LocalBounds.MaxX,
                LocalMaxY = part.LocalBounds.MaxY,
                Entities = part.Entities
                    .Select(entity =>
                    {
                        var flat = new double[entity.Polyline.Count * 2];
                        for (var i = 0; i < entity.Polyline.Count; i++)
                        {
                            flat[i * 2] = entity.Polyline[i].X;
                            flat[i * 2 + 1] = entity.Polyline[i].Y;
                        }

                        return new SceneEntityDto
                        {
                            Id = entity.Id.ToString(),
                            Polyline = flat,
                            ColorHex = ResolveEntityColor(entity),
                        };
                    })
                    .ToList(),
            });
        }

        return scene;
    }

    private string ResolveEntityColor(PartEntity entity)
    {
        if (entity.LayerId is Guid layerId)
        {
            var layer = _layers.FirstOrDefault(l => l.Id == layerId);
            if (layer is not null)
            {
                return layer.ColorHex;
            }
        }

        return FallbackStrokeHex;
    }

    private void SpreadParts(List<PlacedPart> newParts)
    {
        const double spacing = 20;

        var sizes = newParts
            .Select(p => new CornerQueuePacker.Size(
                Math.Max(p.LocalBounds.Width, 1),
                Math.Max(p.LocalBounds.Height, 1)))
            .ToList();

        IReadOnlyList<Bounds2> obstacles;
        Point2 origin;
        Point2? firstCenter;

        if (_parts.Count == 0)
        {
            obstacles = [];
            var viewCenter = GetViewCenterWorld();
            origin = viewCenter;
            firstCenter = viewCenter;
        }
        else
        {
            obstacles = _parts.Select(p => p.GetWorldBounds()).ToList();
            origin = UnionCenter(obstacles);
            firstCenter = null;
        }

        var placements = CornerQueuePacker.Pack(sizes, obstacles, origin, spacing, firstCenter);
        for (var i = 0; i < newParts.Count; i++)
        {
            var pl = placements[i];
            newParts[i].OffsetX = pl.BottomLeftX + pl.Width * 0.5;
            newParts[i].OffsetY = pl.BottomLeftY + pl.Height * 0.5;
        }
    }

    private static Point2 UnionCenter(IReadOnlyList<Bounds2> bounds)
    {
        if (bounds.Count == 0)
        {
            return new Point2(0, 0);
        }

        var minX = bounds.Min(b => b.MinX);
        var minY = bounds.Min(b => b.MinY);
        var maxX = bounds.Max(b => b.MaxX);
        var maxY = bounds.Max(b => b.MaxY);
        return new Point2((minX + maxX) * 0.5, (minY + maxY) * 0.5);
    }

    private static Point2 GetSelectionCentroid(IReadOnlyList<PlacedPart> selected)
    {
        if (selected.Count == 0)
        {
            return new Point2(0, 0);
        }

        var bounds = selected.Select(p => p.GetWorldBounds()).ToList();
        var minX = bounds.Min(b => b.MinX);
        var minY = bounds.Min(b => b.MinY);
        var maxX = bounds.Max(b => b.MaxX);
        var maxY = bounds.Max(b => b.MaxY);
        return new Point2((minX + maxX) * 0.5, (minY + maxY) * 0.5);
    }

    private void Notify() => Changed?.Invoke();
}
