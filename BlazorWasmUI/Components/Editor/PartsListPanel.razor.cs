using BlazorWasmUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorWasmUI.Components.Editor;

public partial class PartsListPanel : IDisposable
{
    protected override void OnInitialized()
    {
        Workspace.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void Select(Guid id, MouseEventArgs e)
    {
        if (e.ShiftKey)
        {
            Workspace.ToggleSelection(id);
            return;
        }

        if (Workspace.SelectedPartIds.Count == 1 && Workspace.SelectedPartIds.Contains(id))
        {
            Workspace.ClearSelection();
            return;
        }

        Workspace.SelectOnly(id);
    }

    public void Dispose() => Workspace.Changed -= OnChanged;
}
