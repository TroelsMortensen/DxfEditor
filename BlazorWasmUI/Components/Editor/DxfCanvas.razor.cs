using BlazorWasmUI.Models;
using BlazorWasmUI.Services;
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
    private bool _infoOpen;

    protected override void OnInitialized()
    {
        Workspace.Changed += OnWorkspaceChanged;
    }

    private void OpenInfo() => _infoOpen = true;

    private void CloseInfo() => _infoOpen = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized)
        {
            return;
        }

        _initialized = true;
        _self = DotNetObjectReference.Create(this);
        // Cache-bust so wwwroot edits reload (import-map fingerprints can stick in-session).
        _module = await Js.InvokeAsync<IJSObjectReference>("import", $"./js/dxfCanvas.js?v={DateTime.UtcNow.Ticks}");
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
    public void OnSelectionChanged(string[] partIds, string[] entityIds)
    {
        static IEnumerable<Guid> ParseIds(string[]? ids) =>
            (ids ?? [])
            .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty);

        Workspace.SetSelection(ParseIds(partIds), ParseIds(entityIds));
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

    [JSInvokable]
    public Task OnDuplicateRequested(double? worldX, double? worldY)
    {
        Workspace.DuplicateSelection(worldX, worldY);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnFilesDropped(DroppedFileDto[] files)
    {
        var decoded = new List<(string Name, byte[] Data)>();
        string? decodeError = null;

        foreach (var file in files)
        {
            var name = string.IsNullOrWhiteSpace(file.Name) ? "file.dxf" : file.Name;
            try
            {
                decoded.Add((name, Convert.FromBase64String(file.DataBase64)));
            }
            catch
            {
                decodeError ??= $"Could not read '{name}'.";
            }
        }

        if (decoded.Count > 0)
        {
            DxfBatchImport.ImportFiles(Workspace, ImportService, decoded);
        }
        else if (decodeError is not null)
        {
            Workspace.SetStatus(decodeError);
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
