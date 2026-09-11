using System.Globalization;
using System.Text;
using BlazorWasmUI.Models;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Tables;
using netDxf.Units;

namespace BlazorWasmUI.Services;

public sealed class DxfExportService
{
    private const string FallbackStrokeHex = "#d7dde5";
    private const string FallbackLayerName = "Default";
    private const double ClosedEpsilon = 1e-9;

    public byte[] ExportToBytes(
        IReadOnlyList<PlacedPart> parts,
        IReadOnlyList<LayerDefinition> layers)
    {
        var doc = new DxfDocument(DxfVersion.AutoCad2000);
        doc.DrawingVariables.InsUnits = DrawingUnits.Millimeters;

        var layerById = new Dictionary<Guid, Layer>();

        foreach (var def in layers)
        {
            var dxfLayer = EnsureLayer(doc, def.Name, def.ColorHex);
            layerById[def.Id] = dxfLayer;
        }

        var fallbackLayer = EnsureLayer(doc, FallbackLayerName, FallbackStrokeHex);
        var usedBlockNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            var blockEntities = new List<EntityObject>();

            foreach (var entity in part.Entities)
            {
                if (entity.Polyline.Count < 2)
                {
                    continue;
                }

                var local = entity.Polyline;
                var isClosed = IsClosed(local);
                IReadOnlyList<Point2> verts = local;
                if (isClosed && local.Count > 2)
                {
                    // Polyline2D closed flag closes first/last; drop duplicate end point.
                    verts = local.Take(local.Count - 1).ToList();
                }

                if (verts.Count < 2)
                {
                    continue;
                }

                var vectors = verts.Select(p => new Vector2(p.X, p.Y));
                var poly = new Polyline2D(vectors, isClosed && verts.Count >= 2)
                {
                    Layer = ResolveLayer(entity, layerById, fallbackLayer),
                    Color = AciColor.ByLayer,
                };

                blockEntities.Add(poly);
            }

            if (blockEntities.Count == 0)
            {
                continue;
            }

            var blockName = AllocateBlockName(part.Name, usedBlockNames);
            var block = new Block(blockName, blockEntities);
            doc.Blocks.Add(block);

            var insert = new Insert(block, new Vector2(part.OffsetX, part.OffsetY))
            {
                Rotation = part.RotationDegrees,
                Scale = part.Mirrored
                    ? new Vector3(-1.0, 1.0, 1.0)
                    : new Vector3(1.0, 1.0, 1.0),
            };

            doc.Entities.Add(insert);
        }

        using var ms = new MemoryStream();
        if (!doc.Save(ms, isBinary: false))
        {
            throw new InvalidOperationException("Failed to write DXF document.");
        }

        return ms.ToArray();
    }

    private static Layer ResolveLayer(
        PartEntity entity,
        IReadOnlyDictionary<Guid, Layer> layerById,
        Layer fallback)
    {
        if (entity.LayerId is Guid id && layerById.TryGetValue(id, out var layer))
        {
            return layer;
        }

        return fallback;
    }

    private static Layer EnsureLayer(DxfDocument doc, string name, string colorHex)
    {
        var safeName = SanitizeTableName(name, FallbackLayerName);
        if (doc.Layers.Contains(safeName))
        {
            return doc.Layers[safeName];
        }

        var layer = new Layer(safeName)
        {
            Color = ColorFromHex(colorHex),
        };
        return doc.Layers.Add(layer);
    }

    private static string AllocateBlockName(string partName, HashSet<string> used)
    {
        var baseName = SanitizeTableName(partName, "Part");
        if (used.Add(baseName))
        {
            return baseName;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName}_{i}";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static string SanitizeTableName(string name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name.Trim())
        {
            if (TableObject.InvalidCharacters.Contains(ch)
                || ch is '<' or '>' or '/' or '\\' or '"' or ':' or ';' or '?' or '*' or '|' or '=' or '`' or '\'')
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(ch);
            }
        }

        var sanitized = sb.ToString().Trim();
        if (string.IsNullOrEmpty(sanitized) || !TableObject.IsValidName(sanitized))
        {
            return fallback;
        }

        return sanitized;
    }

    private static AciColor ColorFromHex(string colorHex)
    {
        var hex = LayerPalette.NormalizeHex(colorHex).TrimStart('#');
        if (hex.Length != 6
            || !byte.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return AciColor.FromCadIndex(7);
        }

        var trueColor = (r << 16) | (g << 8) | b;
        return AciColor.FromTrueColor(trueColor);
    }

    private static bool IsClosed(IReadOnlyList<Point2> points)
    {
        if (points.Count < 3)
        {
            return false;
        }

        var a = points[0];
        var b = points[^1];
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy) <= ClosedEpsilon * ClosedEpsilon;
    }
}
