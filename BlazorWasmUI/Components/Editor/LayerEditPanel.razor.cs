using BlazorWasmUI.Models;
using BlazorWasmUI.Services;

namespace BlazorWasmUI.Components.Editor;

public partial class LayerEditPanel : IDisposable
{
    protected override void OnInitialized()
    {
        Workspace.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private bool IsColorUsed(string colorHex)
    {
        var hex = LayerPalette.NormalizeHex(colorHex);
        return Workspace.Layers.Any(l =>
            string.Equals(l.ColorHex, hex, StringComparison.OrdinalIgnoreCase));
    }

    private void Add(string colorHex) => Workspace.AddLayerFromPalette(colorHex);

    public void Dispose() => Workspace.Changed -= OnChanged;
}
