using BlazorWasmUI.Models;

namespace BlazorWasmUI.Services.History;

public sealed class DeletePartsCommand : IEditorCommand
{
    private readonly IReadOnlyList<IndexedPart> _parts;

    public DeletePartsCommand(IReadOnlyList<IndexedPart> parts)
    {
        _parts = parts;
    }

    public void Undo(WorkspaceState ws) =>
        ws.InsertPartsAt(_parts.Select(p => (p.Index, p.Part)));

    public void Redo(WorkspaceState ws) =>
        ws.RemovePartsByIds(_parts.Select(p => p.Part.Id));
}

public sealed record IndexedPart(int Index, PlacedPart Part);
