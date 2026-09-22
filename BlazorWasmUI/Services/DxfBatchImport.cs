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
        // Snap to LightBurn RGB; merge entries that collapse to the same color.
        // Prefer a real imported layer name over the generic "Layer" placeholder.
        var bySnappedHex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void OfferLayer(string name, string colorHex)
        {
            var snapped = LayerPalette.NearestLightBurnHex(colorHex);
            if (bySnappedHex.TryGetValue(snapped, out var existingName))
            {
                if (string.Equals(existingName, "Layer", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, "Layer", StringComparison.OrdinalIgnoreCase))
                {
                    bySnappedHex[snapped] = name;
                }

                return;
            }

            bySnappedHex[snapped] = string.IsNullOrWhiteSpace(name) ? "Layer" : name.Trim();
        }

        foreach (var layer in importedLayers)
        {
            OfferLayer(layer.Name, layer.ColorHex);
        }

        foreach (var entity in part.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.SourceColorHex))
            {
                continue;
            }

            OfferLayer("Layer", entity.SourceColorHex);
        }

        if (bySnappedHex.Count > 0)
        {
            workspace.EnsureLayers(bySnappedHex.Select(kv => (kv.Value, kv.Key)));
        }

        var layerIdByHex = workspace.Layers.ToDictionary(
            l => l.ColorHex,
            l => l.Id,
            StringComparer.OrdinalIgnoreCase);

        foreach (var entity in part.Entities)
        {
            if (!string.IsNullOrWhiteSpace(entity.SourceColorHex))
            {
                var snapped = LayerPalette.NearestLightBurnHex(entity.SourceColorHex);
                if (layerIdByHex.TryGetValue(snapped, out var layerId))
                {
                    entity.LayerId = layerId;
                }
            }

            entity.SourceColorHex = null;
        }
    }
}
