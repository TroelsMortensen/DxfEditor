using BlazorWasmUI.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorWasmUI.Components.Editor;

public partial class DxfCanvas
{
    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private IJSObjectReference? _controller;
    private DotNetObjectReference<DxfCanvas>? _self;
    private bool _initialized;
    private bool _pushing;
    private bool _pushQueued;

    protected override void OnInitialized()
    {
        Workspace.Changed += OnWorkspaceChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized)
        {
            return;
        }

        _initialized = true;
        _self = DotNetObjectReference.Create(this);
        _module = await Js.InvokeAsync<IJSObjectReference>("import", "./js/dxfCanvas.js");
        _controller = await _module.InvokeAsync<IJSObjectReference>("createCanvasController", _canvas, _self);
        await PushSceneAsync();
    }

    private void OnWorkspaceChanged()
    {
        _ = InvokeAsync(async () =>
        {
            await PushSceneAsync();
            StateHasChanged();
        });
    }

    private async Task PushSceneAsync()
    {
        if (_controller is null)
        {
            return;
        }

        if (_pushing)
        {
            // Another push is in flight (e.g. EnsureLayers then AddParts).
            // Queue a follow-up so the latest workspace state is not dropped.
            _pushQueued = true;
            return;
        }

        _pushing = true;
        try
        {
            do
            {
                _pushQueued = false;
                var scene = Workspace.BuildScene();
                await _controller.InvokeVoidAsync("setScene", scene);
            } while (_pushQueued);
        }
        finally
        {
            _pushing = false;
        }
    }

    [JSInvokable]
    public void OnSelectionChanged(string[] ids)
    {
        var guids = ids
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty);
        Workspace.SetSelection(guids);
    }

    [JSInvokable]
    public void OnPartsTransformed(PartTransformDto[] transforms)
    {
        Workspace.ApplyTransforms(transforms);
    }

    [JSInvokable]
    public void OnViewportChanged(double panX, double panY, double zoom)
    {
        Workspace.SetViewport(panX, panY, zoom);
    }

    [JSInvokable]
    public async Task OnDeleteRequested()
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

    [JSInvokable]
    public Task OnFilesDropped(DroppedFileDto[] files)
    {
        var imported = new List<PlacedPart>();
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var file in files)
        {
            var name = string.IsNullOrWhiteSpace(file.Name) ? "file.dxf" : file.Name;
            byte[] data;
            try
            {
                data = Convert.FromBase64String(file.DataBase64);
            }
            catch
            {
                errors.Add($"Could not read '{name}'.");
                continue;
            }

            var result = ImportService.ImportFromBytes(data, name);
            if (result.Success && result.Part is not null)
            {
                Guid? dominantLayerId = null;
                if (result.Layers.Count > 0)
                {
                    var ensured = Workspace.EnsureLayers(
                        result.Layers.Select(l => (l.Name, l.ColorHex)));
                    dominantLayerId = ensured.Count > 0 ? ensured[0].Id : null;
                }

                if (dominantLayerId is Guid layerId)
                {
                    result.Part.LayerId = layerId;
                }

                imported.Add(result.Part);
                if (result.Warnings.Count > 0)
                {
                    warnings.AddRange(result.Warnings);
                }
            }
            else if (!string.IsNullOrWhiteSpace(result.Error))
            {
                errors.Add(result.Error);
            }
        }

        if (imported.Count > 0)
        {
            Workspace.AddParts(imported, spreadEvenly: true);
            var status = $"Imported {imported.Count} file(s).";
            if (warnings.Count > 0)
            {
                status = $"{status} {warnings[0]}";
            }

            Workspace.SetStatus(status);
        }
        else if (errors.Count > 0)
        {
            Workspace.SetStatus(errors[0]);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        Workspace.Changed -= OnWorkspaceChanged;
        try
        {
            if (_controller is not null)
            {
                await _controller.InvokeVoidAsync("dispose");
                await _controller.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // app shutting down
        }

        _self?.Dispose();
    }
}
