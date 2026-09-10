using BlazorWasmUI.Models;

namespace BlazorWasmUI.Services;

public sealed class WorkspaceState
{
    private const string FallbackStrokeHex = "#d7dde5";

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

    public string? StatusMessage { get; private set; }

    public event Action? Changed;

    public void SetStatus(string? message)
    {
        StatusMessage = message;
        Notify();
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

    public void SetViewport(double panX, double panY, double zoom)
    {
        var clamped = Math.Clamp(zoom, 0.05, 50);
        if (Math.Abs(PanX - panX) < 1e-9 && Math.Abs(PanY - panY) < 1e-9 && Math.Abs(Zoom - clamped) < 1e-12)
        {
            return;
        }

        PanX = panX;
        PanY = panY;
        Zoom = clamped;
        Notify();
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
            StatusMessage = removed == 1 ? "Deleted 1 part." : $"Deleted {removed} parts.";
            Notify();
        }

        return removed;
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
        const double gap = 20;
        const double maxRowWidth = 800;

        var cursorX = 0.0;
        var rowY = 0.0;
        var rowHeight = 0.0;
        var rowStartX = 0.0;

        if (_parts.Count > 0)
        {
            var existingMaxX = _parts.Max(p => p.GetWorldBounds().MaxX);
            var existingMinY = _parts.Min(p => p.GetWorldBounds().MinY);
            cursorX = existingMaxX + gap;
            rowStartX = cursorX;
            rowY = existingMinY;
        }

        foreach (var part in newParts)
        {
            var w = Math.Max(part.LocalBounds.Width, 1);
            var h = Math.Max(part.LocalBounds.Height, 1);

            if (cursorX - rowStartX > maxRowWidth && cursorX > rowStartX)
            {
                cursorX = rowStartX;
                rowY += rowHeight + gap;
                rowHeight = 0;
            }

            part.OffsetX = cursorX + w * 0.5;
            part.OffsetY = rowY + h * 0.5;
            cursorX += w + gap;
            rowHeight = Math.Max(rowHeight, h);
        }
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
