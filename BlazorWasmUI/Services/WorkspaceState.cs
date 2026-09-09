using BlazorWasmUI.Models;

namespace BlazorWasmUI.Services;

public sealed class WorkspaceState
{
    private readonly List<PlacedPart> _parts = [];
    private readonly HashSet<Guid> _selectedIds = [];

    public IReadOnlyList<PlacedPart> Parts => _parts;
    public IReadOnlyCollection<Guid> SelectedIds => _selectedIds;

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

    public void SetSelection(IEnumerable<Guid> ids, bool clearExisting = true)
    {
        if (clearExisting)
        {
            _selectedIds.Clear();
        }

        foreach (var id in ids)
        {
            if (_parts.Any(p => p.Id == id))
            {
                _selectedIds.Add(id);
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

        if (!_selectedIds.Add(id))
        {
            _selectedIds.Remove(id);
        }

        Notify();
    }

    public void SelectOnly(Guid id)
    {
        _selectedIds.Clear();
        if (_parts.Any(p => p.Id == id))
        {
            _selectedIds.Add(id);
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
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var selected = _parts.Where(p => _selectedIds.Contains(p.Id)).ToList();
        var centroid = GetSelectionCentroid(selected);

        foreach (var part in selected)
        {
            // Reflect part origin about selection centroid in X, then flip local mirror flag.
            part.OffsetX = 2 * centroid.X - part.OffsetX;
            part.Mirrored = !part.Mirrored;
            part.RotationDegrees = -part.RotationDegrees;
        }

        Notify();
    }

    public void MirrorSelectionVertical()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var selected = _parts.Where(p => _selectedIds.Contains(p.Id)).ToList();
        var centroid = GetSelectionCentroid(selected);

        foreach (var part in selected)
        {
            // Reflect part origin about selection centroid in Y, then compose a vertical flip.
            part.OffsetY = 2 * centroid.Y - part.OffsetY;
            part.Mirrored = !part.Mirrored;
            part.RotationDegrees = 180 - part.RotationDegrees;
        }

        Notify();
    }

    public int DeleteSelection()
    {
        if (_selectedIds.Count == 0)
        {
            return 0;
        }

        var removed = _parts.RemoveAll(p => _selectedIds.Contains(p.Id));
        _selectedIds.Clear();
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
            SelectedIds = _selectedIds.Select(id => id.ToString()).ToList(),
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
                Polylines = part.LocalPolylines
                    .Select(poly =>
                    {
                        var flat = new double[poly.Count * 2];
                        for (var i = 0; i < poly.Count; i++)
                        {
                            flat[i * 2] = poly[i].X;
                            flat[i * 2 + 1] = poly[i].Y;
                        }

                        return flat;
                    })
                    .ToList(),
            });
        }

        return scene;
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
