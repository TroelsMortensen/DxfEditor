using BlazorWasmUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorWasmUI.Components.Editor;

public partial class LeftToolsPanel : IDisposable
{
    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    protected override void OnInitialized()
    {
        Workspace.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void Mirror() => Workspace.MirrorSelectionHorizontal();

    private async Task DeleteAsync()
    {
        if (Workspace.SelectedIds.Count == 0)
        {
            return;
        }

        var count = Workspace.SelectedIds.Count;
        var message = count == 1
            ? "Delete the selected part?"
            : $"Delete the {count} selected parts?";

        var confirmed = await Js.InvokeAsync<bool>("confirm", message);
        if (!confirmed)
        {
            return;
        }

        Workspace.DeleteSelection();
    }

    public void Dispose() => Workspace.Changed -= OnChanged;
}
