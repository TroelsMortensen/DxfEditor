using BlazorWasmUI.Models;

namespace BlazorWasmUI.Services;

public static class DxfBatchImport
{
    public static void ImportFiles(
        WorkspaceState workspace,
        DxfImportService importService,
        IEnumerable<(string Name, byte[] Data)> files)
    {
        var imported = new List<PlacedPart>();
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var (rawName, data) in files)
        {
            var name = string.IsNullOrWhiteSpace(rawName) ? "file.dxf" : rawName;
            var result = importService.ImportFromBytes(data, name);
            if (result.Success && result.Part is not null)
            {
                Guid? dominantLayerId = null;
                if (result.Layers.Count > 0)
                {
                    var ensured = workspace.EnsureLayers(
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
            workspace.AddParts(imported, spreadEvenly: true);
            var status = $"Imported {imported.Count} file(s).";
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
}
