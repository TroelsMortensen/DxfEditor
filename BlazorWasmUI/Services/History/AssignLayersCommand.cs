namespace BlazorWasmUI.Services.History;

public sealed class AssignLayersCommand : IEditorCommand
{
    private readonly IReadOnlyList<LayerAssignDelta> _deltas;

    public AssignLayersCommand(IReadOnlyList<LayerAssignDelta> deltas)
    {
        _deltas = deltas;
    }

    public void Undo(WorkspaceState ws) =>
        ws.AssignEntityLayers(_deltas.Select(d => (d.EntityId, d.BeforeLayerId)));

    public void Redo(WorkspaceState ws) =>
        ws.AssignEntityLayers(_deltas.Select(d => (d.EntityId, d.AfterLayerId)));
}

public readonly record struct LayerAssignDelta(
    Guid EntityId,
    Guid? BeforeLayerId,
    Guid? AfterLayerId);
