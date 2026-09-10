using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorWasmUI.Components.Editor;

public partial class InfoHelpModal
{
    private ElementReference _root;
    private bool _wasOpen;

    [Parameter]
    public bool IsOpen { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsOpen && !_wasOpen)
        {
            try
            {
                await _root.FocusAsync();
            }
            catch
            {
                // Element may not be focusable yet in edge cases.
            }
        }

        _wasOpen = IsOpen;
    }

    private Task CloseAsync() => OnClose.InvokeAsync();

    private async Task OnDialogKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            await CloseAsync();
        }
    }
}
