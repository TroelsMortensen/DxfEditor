using BlazorWasmUI.Services;

namespace BlazorWasmUI.Components.Editor;

public partial class LayerBarPanel : IDisposable
{
    protected override void OnInitialized()
    {
        Workspace.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void Assign(Guid layerId) => Workspace.AssignSelectionToLayer(layerId);

    private bool IsActiveLayer(Guid layerId)
    {
        if (Workspace.SelectedIds.Count == 0)
        {
            return false;
        }

        var selected = Workspace.Parts.Where(p => Workspace.SelectedIds.Contains(p.Id)).ToList();
        return selected.Count > 0 && selected.All(p => p.LayerId == layerId);
    }

    public void Dispose() => Workspace.Changed -= OnChanged;
}
