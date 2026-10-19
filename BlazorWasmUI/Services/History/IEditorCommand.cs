namespace BlazorWasmUI.Services.History;

public interface IEditorCommand
{
    void Undo(WorkspaceState ws);
    void Redo(WorkspaceState ws);
}
