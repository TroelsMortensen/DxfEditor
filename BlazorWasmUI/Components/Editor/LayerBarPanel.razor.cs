using BlazorWasmUI.Services;
using BlazorWasmUI.Services.History;

namespace BlazorWasmUI.Components.Editor;

public partial class LayerBarPanel : IDisposable
{
    protected override void OnInitialized()
    {
        Workspace.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void Assign(Guid layerId) => History.AssignSelectionToLayer(layerId);

    private bool IsActiveLayer(Guid layerId)
    {
        var targets = Workspace.GetLayerAssignmentTargets();
        return targets.Count > 0 && targets.All(e => e.LayerId == layerId);
    }

    public void Dispose() => Workspace.Changed -= OnChanged;
}
