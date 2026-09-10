using BlazorWasmUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace BlazorWasmUI.Components.Editor;

public partial class LeftToolsPanel : IDisposable
{
    private const long MaxFileBytes = 50L * 1024 * 1024;

    private int _importInputKey;

    [Inject]
    private IJSRuntime Js { get; set; } = default!;

    protected override void OnInitialized()
    {
        Workspace.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void MirrorHorizontal() => Workspace.MirrorSelectionHorizontal();

    private void MirrorVertical() => Workspace.MirrorSelectionVertical();

    private async Task OnFilesSelected(InputFileChangeEventArgs e)
    {
        var files = new List<(string Name, byte[] Data)>();

        foreach (var file in e.GetMultipleFiles(100))
        {
            if (!file.Name.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                await using var stream = file.OpenReadStream(MaxFileBytes);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                files.Add((file.Name, ms.ToArray()));
            }
            catch
            {
                Workspace.SetStatus($"Could not read '{file.Name}'.");
            }
        }

        if (files.Count > 0)
        {
            DxfBatchImport.ImportFiles(Workspace, ImportService, files);
        }

        _importInputKey++;
    }

    private async Task DeleteAsync()
    {
        var editable = Workspace.GetEditableParts();
        if (editable.Count == 0)
        {
            return;
        }

        var count = editable.Count;
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
