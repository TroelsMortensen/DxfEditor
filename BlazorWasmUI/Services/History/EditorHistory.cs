using BlazorWasmUI.Models;

namespace BlazorWasmUI.Services.History;

public sealed class EditorHistory
{
    private const int MaxDepth = 50;

    private readonly WorkspaceState _workspace;
    private readonly List<IEditorCommand> _undo = [];
    private readonly List<IEditorCommand> _redo = [];
    private bool _isApplying;

    public EditorHistory(WorkspaceState workspace)
    {
        _workspace = workspace;
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public event Action? Changed;

    public void RecordTransforms(IEnumerable<PartTransformDto> after)
    {
        var afterList = after.ToList();
        if (afterList.Count == 0)
        {
            return;
        }

        var beforeDeltas = new List<PartPoseDelta>();
        var afterDeltas = new List<PartPoseDelta>();

        foreach (var t in afterList)
        {
            if (!Guid.TryParse(t.Id, out var id))
            {
                continue;
            }

            var part = _workspace.Parts.FirstOrDefault(p => p.Id == id);
            if (part is null)
            {
                continue;
            }

            var before = CapturePose(part);
            var afterPose = new PartPoseDelta(id, t.OffsetX, t.OffsetY, t.RotationDegrees, t.Mirrored);
            if (PosesEqual(before, afterPose))
            {
                continue;
            }

            beforeDeltas.Add(before);
            afterDeltas.Add(afterPose);
        }

        _workspace.ApplyTransforms(afterList);

        if (beforeDeltas.Count > 0)
        {
            Push(new TransformPartsCommand(beforeDeltas, afterDeltas));
        }
    }

    public void MirrorSelectionHorizontal()
    {
        RecordMirror(() => _workspace.MirrorSelectionHorizontal());
    }

    public void MirrorSelectionVertical()
    {
        RecordMirror(() => _workspace.MirrorSelectionVertical());
    }

    public int DuplicateSelection(double? placeAtWorldX = null, double? placeAtWorldY = null)
    {
        var beforeIds = _workspace.Parts.Select(p => p.Id).ToHashSet();
        var count = _workspace.DuplicateSelection(placeAtWorldX, placeAtWorldY);
        if (count == 0)
        {
            return 0;
        }

        var created = new List<IndexedPart>();
        for (var i = 0; i < _workspace.Parts.Count; i++)
        {
            var part = _workspace.Parts[i];
            if (!beforeIds.Contains(part.Id))
            {
                created.Add(new IndexedPart(i, ClonePart(part)));
            }
        }

        if (created.Count > 0)
        {
            Push(new DuplicatePartsCommand(created));
        }

        return count;
    }

    public void AssignSelectionToLayer(Guid layerId)
    {
        var targets = _workspace.GetLayerAssignmentTargets();
        if (targets.Count == 0)
        {
            return;
        }

        var before = targets
            .Select(e => (e.Id, e.LayerId))
            .ToList();

        _workspace.AssignSelectionToLayer(layerId);

        var deltas = new List<LayerAssignDelta>();
        foreach (var (entityId, beforeLayerId) in before)
        {
            var entity = _workspace.Parts
                .SelectMany(p => p.Entities)
                .FirstOrDefault(e => e.Id == entityId);
            if (entity is null || entity.LayerId == beforeLayerId)
            {
                continue;
            }

            deltas.Add(new LayerAssignDelta(entityId, beforeLayerId, entity.LayerId));
        }

        if (deltas.Count > 0)
        {
            Push(new AssignLayersCommand(deltas));
        }
    }

    public int DeleteSelection()
    {
        var selected = _workspace.GetEditableParts();
        if (selected.Count == 0)
        {
            return 0;
        }

        var indexed = new List<IndexedPart>();
        for (var i = 0; i < _workspace.Parts.Count; i++)
        {
            var part = _workspace.Parts[i];
            if (selected.Any(s => s.Id == part.Id))
            {
                indexed.Add(new IndexedPart(i, ClonePart(part)));
            }
        }

        var removed = _workspace.DeleteSelection();
        if (removed > 0 && indexed.Count > 0)
        {
            Push(new DeletePartsCommand(indexed));
        }

        return removed;
    }

    public void Undo()
    {
        if (_undo.Count == 0 || _isApplying)
        {
            return;
        }

        var cmd = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _isApplying = true;
        try
        {
            cmd.Undo(_workspace);
        }
        finally
        {
            _isApplying = false;
        }

        _redo.Add(cmd);
        Notify();
    }

    public void Redo()
    {
        if (_redo.Count == 0 || _isApplying)
        {
            return;
        }

        var cmd = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _isApplying = true;
        try
        {
            cmd.Redo(_workspace);
        }
        finally
        {
            _isApplying = false;
        }

        _undo.Add(cmd);
        TrimUndo();
        Notify();
    }

    private void RecordMirror(Action apply)
    {
        var selected = _workspace.GetEditableParts();
        if (selected.Count == 0)
        {
            return;
        }

        var before = selected.Select(CapturePose).ToList();
        apply();
        var after = selected.Select(CapturePose).ToList();

        var beforeDeltas = new List<PartPoseDelta>();
        var afterDeltas = new List<PartPoseDelta>();
        for (var i = 0; i < before.Count; i++)
        {
            if (PosesEqual(before[i], after[i]))
            {
                continue;
            }

            beforeDeltas.Add(before[i]);
            afterDeltas.Add(after[i]);
        }

        if (beforeDeltas.Count > 0)
        {
            Push(new TransformPartsCommand(beforeDeltas, afterDeltas));
        }
    }

    private void Push(IEditorCommand command)
    {
        if (_isApplying)
        {
            return;
        }

        _undo.Add(command);
        _redo.Clear();
        TrimUndo();
        Notify();
    }

    private void TrimUndo()
    {
        while (_undo.Count > MaxDepth)
        {
            _undo.RemoveAt(0);
        }
    }

    private void Notify() => Changed?.Invoke();

    private static PartPoseDelta CapturePose(PlacedPart part) =>
        new(part.Id, part.OffsetX, part.OffsetY, part.RotationDegrees, part.Mirrored);

    private static bool PosesEqual(PartPoseDelta a, PartPoseDelta b) =>
        a.PartId == b.PartId
        && a.OffsetX == b.OffsetX
        && a.OffsetY == b.OffsetY
        && a.RotationDegrees == b.RotationDegrees
        && a.Mirrored == b.Mirrored;

    private static PlacedPart ClonePart(PlacedPart source) =>
        new()
        {
            Id = source.Id,
            Name = source.Name,
            Entities = source.Entities
                .Select(e => new PartEntity
                {
                    Id = e.Id,
                    Polyline = e.Polyline.ToList(),
                    LayerId = e.LayerId,
                    SourceColorHex = e.SourceColorHex,
                })
                .ToList(),
            LocalBounds = source.LocalBounds,
            OffsetX = source.OffsetX,
            OffsetY = source.OffsetY,
            RotationDegrees = source.RotationDegrees,
            Mirrored = source.Mirrored,
        };
}
