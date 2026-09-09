using BlazorWasmUI.Services;
using Microsoft.AspNetCore.Components;

namespace BlazorWasmUI.Components.Editor;

public partial class PartsListPanel : IDisposable
{
    protected override void OnInitialized()
    {
        Workspace.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void Select(Guid id) => Workspace.SelectOnly(id);

    public void Dispose() => Workspace.Changed -= OnChanged;
}
