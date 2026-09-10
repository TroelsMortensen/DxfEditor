using BlazorWasmUI.Models;
using BlazorWasmUI.Services;
using Microsoft.AspNetCore.Components.Web;

namespace BlazorWasmUI.Components.Editor;

public partial class PartsListPanel : IDisposable
{
    private Guid? _anchorId;

    protected override void OnInitialized()
    {
        Workspace.Changed += OnChanged;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private void Select(Guid id, MouseEventArgs e)
    {
        if (e.CtrlKey || e.MetaKey)
        {
            Workspace.ToggleSelection(id);
            _anchorId = id;
            return;
        }

        if (e.ShiftKey)
        {
            SelectRange(id);
            return;
        }

        if (Workspace.SelectedPartIds.Count == 1 && Workspace.SelectedPartIds.Contains(id))
        {
            Workspace.ClearSelection();
            return;
        }

        Workspace.SelectOnly(id);
        _anchorId = id;
    }

    private void SelectRange(Guid id)
    {
        var parts = Workspace.Parts;
        var clickIndex = IndexOf(parts, id);
        if (clickIndex < 0)
        {
            return;
        }

        var anchorIndex = ResolveAnchorIndex(parts);
        if (anchorIndex < 0)
        {
            Workspace.SelectOnly(id);
            _anchorId = id;
            return;
        }

        var from = Math.Min(anchorIndex, clickIndex);
        var to = Math.Max(anchorIndex, clickIndex);
        var rangeIds = parts.Skip(from).Take(to - from + 1).Select(p => p.Id);
        Workspace.SetPartSelection(rangeIds);
    }

    private int ResolveAnchorIndex(IReadOnlyList<PlacedPart> parts)
    {
        if (_anchorId is Guid anchorId)
        {
            var index = IndexOf(parts, anchorId);
            if (index >= 0)
            {
                return index;
            }
        }

        var lowestSelected = -1;
        for (var i = 0; i < parts.Count; i++)
        {
            if (!Workspace.SelectedPartIds.Contains(parts[i].Id))
            {
                continue;
            }

            if (lowestSelected < 0 || i < lowestSelected)
            {
                lowestSelected = i;
            }
        }

        return lowestSelected;
    }

    private static int IndexOf(IReadOnlyList<PlacedPart> parts, Guid id)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public void Dispose() => Workspace.Changed -= OnChanged;
}
