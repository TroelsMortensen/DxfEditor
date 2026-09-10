using System.Globalization;
using System.Text;
using BlazorWasmUI.Models;
using netDxf;
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

        foreach (var part in parts)
        {
            foreach (var entity in part.Entities)
            {
                if (entity.Polyline.Count < 2)
                {
                    continue;
                }

                var world = entity.Polyline
                    .Select(part.TransformLocalToWorld)
                    .ToList();

                var isClosed = IsClosed(world);
                IReadOnlyList<Point2> verts = world;
                if (isClosed && world.Count > 2)
                {
                    // Polyline2D closed flag closes first/last; drop duplicate end point.
                    verts = world.Take(world.Count - 1).ToList();
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

                doc.Entities.Add(poly);
            }
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
        var safeName = SanitizeLayerName(name);
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

    private static string SanitizeLayerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return FallbackLayerName;
        }

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name.Trim())
        {
            // DXF layer names disallow <>/\":;?*|=`'
            if (ch is '<' or '>' or '/' or '\\' or '"' or ':' or ';' or '?' or '*' or '|' or '=' or '`' or '\'')
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(ch);
            }
        }

        var sanitized = sb.ToString().Trim();
        return string.IsNullOrEmpty(sanitized) ? FallbackLayerName : sanitized;
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
