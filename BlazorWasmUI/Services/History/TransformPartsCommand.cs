using BlazorWasmUI.Models;

namespace BlazorWasmUI.Services.History;

public sealed class TransformPartsCommand(
    IReadOnlyList<PartPoseDelta> before,
    IReadOnlyList<PartPoseDelta> after)
    : IEditorCommand
{
    public void Undo(WorkspaceState ws) => ws.ApplyTransforms(ToDtos(before));

    public void Redo(WorkspaceState ws) => ws.ApplyTransforms(ToDtos(after));

    private static IEnumerable<PartTransformDto> ToDtos(IReadOnlyList<PartPoseDelta> deltas) =>
        deltas.Select(d => new PartTransformDto
        {
            Id = d.PartId.ToString(),
            OffsetX = d.OffsetX,
            OffsetY = d.OffsetY,
            RotationDegrees = d.RotationDegrees,
            Mirrored = d.Mirrored,
        });
}

public readonly record struct PartPoseDelta(
    Guid PartId,
    double OffsetX,
    double OffsetY,
    double RotationDegrees,
    bool Mirrored);
