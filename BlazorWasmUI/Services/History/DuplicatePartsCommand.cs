namespace BlazorWasmUI.Services.History;

public sealed class DuplicatePartsCommand : IEditorCommand
{
    private readonly IReadOnlyList<IndexedPart> _created;

    public DuplicatePartsCommand(IReadOnlyList<IndexedPart> created)
    {
        _created = created;
    }

    public void Undo(WorkspaceState ws) =>
        ws.RemovePartsByIds(_created.Select(p => p.Part.Id));

    public void Redo(WorkspaceState ws) =>
        ws.InsertPartsAt(_created.Select(p => (p.Index, p.Part)));
}
