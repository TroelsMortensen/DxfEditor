using BlazorWasmUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using System.Text.Json.Serialization;

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

    private void Duplicate() => Workspace.DuplicateSelection();

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

    private async Task ExportAsync()
    {
        if (Workspace.Parts.Count == 0)
        {
            return;
        }

        try
        {
            var bytes = ExportService.ExportToBytes(Workspace.Parts, Workspace.Layers);
            var result = await Js.InvokeAsync<SaveDxfResult>(
                "dxfFile.saveDxf",
                bytes,
                "layout.dxf");

            if (result.Cancelled)
            {
                return;
            }

            if (!result.Ok)
            {
                Workspace.SetStatus(
                    string.IsNullOrWhiteSpace(result.Error)
                        ? "Export failed."
                        : $"Export failed: {result.Error}");
                return;
            }

            Workspace.SetStatus("Exported master DXF (parts as blocks).");
        }
        catch (Exception ex)
        {
            Workspace.SetStatus($"Export failed: {ex.Message}");
        }
    }

    private sealed class SaveDxfResult
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("cancelled")]
        public bool Cancelled { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public void Dispose() => Workspace.Changed -= OnChanged;
}
