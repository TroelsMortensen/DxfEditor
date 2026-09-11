using BlazorWasmUI.Models;

namespace BlazorWasmUI.Services;

public static class DxfBatchImport
{
    public static void ImportFiles(
        WorkspaceState workspace,
        DxfImportService importService,
        IEnumerable<(string Name, byte[] Data)> files)
    {
        var layoutParts = new List<PlacedPart>();
        var spreadParts = new List<PlacedPart>();
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var (rawName, data) in files)
        {
            var name = string.IsNullOrWhiteSpace(rawName) ? "file.dxf" : rawName;
            var result = importService.ImportFromBytes(data, name);
            if (result.Success && result.Parts.Count > 0)
            {
                foreach (var part in result.Parts)
                {
                    AssignEntityLayers(workspace, part, result.Layers);
                    if (result.PreserveWorldLayout)
                    {
                        layoutParts.Add(part);
                    }
                    else
                    {
                        spreadParts.Add(part);
                    }
                }

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

        var total = layoutParts.Count + spreadParts.Count;
        if (total > 0)
        {
            if (layoutParts.Count > 0)
            {
                workspace.AddParts(layoutParts, spreadEvenly: false);
            }

            if (spreadParts.Count > 0)
            {
                workspace.AddParts(spreadParts, spreadEvenly: true);
            }

            var status = $"Imported {total} part(s).";
            if (warnings.Count > 0)
            {
                status = $"{status} {warnings[0]}";
            }

            workspace.SetStatus(status);
        }
        else if (errors.Count > 0)
        {
            workspace.SetStatus(errors[0]);
        }
    }

    private static void AssignEntityLayers(
        WorkspaceState workspace,
        PlacedPart part,
        IReadOnlyList<ImportedLayerInfo> importedLayers)
    {
        var ensureList = new List<(string Name, string ColorHex)>();
        if (importedLayers.Count > 0)
        {
            ensureList.AddRange(importedLayers.Select(l => (l.Name, l.ColorHex)));
        }

        foreach (var entity in part.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.SourceColorHex))
            {
                continue;
            }

            var hex = LayerPalette.NormalizeHex(entity.SourceColorHex);
            if (ensureList.All(l =>
                    !string.Equals(LayerPalette.NormalizeHex(l.ColorHex), hex, StringComparison.OrdinalIgnoreCase)))
            {
                ensureList.Add(("Layer", hex));
            }
        }

        if (ensureList.Count > 0)
        {
            workspace.EnsureLayers(ensureList);
        }

        var byHex = workspace.Layers.ToDictionary(
            l => l.ColorHex,
            l => l.Id,
            StringComparer.OrdinalIgnoreCase);

        foreach (var entity in part.Entities)
        {
            if (!string.IsNullOrWhiteSpace(entity.SourceColorHex))
            {
                var hex = LayerPalette.NormalizeHex(entity.SourceColorHex);
                if (byHex.TryGetValue(hex, out var layerId))
                {
                    entity.LayerId = layerId;
                }
            }

            entity.SourceColorHex = null;
        }
    }
}
