using System.Text;
using System.Text.RegularExpressions;
using BlazorWasmUI.Models;
using netDxf;
using netDxf.Entities;
using netDxf.Header;

namespace BlazorWasmUI.Services;

public sealed class DxfImportService
{
    private const int ArcSegments = 48;
    private static readonly Regex AcadVerValue = new(
        @"^\s*AC1\d{3}\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public ImportResult ImportFromBytes(byte[] data, string fileName)
    {
        var warnings = new List<string>();

        try
        {
            using var source = new MemoryStream(data, writable: false);
            source.Position = 0;

            var version = DxfDocument.CheckDxfFileVersion(source, out var isBinary);
            source.Position = 0;

            MemoryStream loadStream;
            if (version != DxfVersion.Unknown && version < DxfVersion.AutoCad2000)
            {
                if (isBinary)
                {
                    return ImportResult.Fail(
                        $"'{fileName}' is AutoCAD {FormatVersion(version)} (binary). " +
                        "netDxf requires AutoCAD 2000+ ASCII DXF. Re-save as AutoCAD 2000 or newer ASCII DXF from your CAD tool.");
                }

                loadStream = UpgradeAcadVersionTo2000(source, version, fileName, warnings);
            }
            else
            {
                loadStream = new MemoryStream(data, writable: false);
            }

            using (loadStream)
            {
                loadStream.Position = 0;

                // Seekable in-memory stream only — never a filesystem path (WASM).
                using var reader = new StreamReader(
                    loadStream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true);
                _ = reader.Peek();
                loadStream.Position = 0;

                DxfDocument? doc;
                try
                {
                    doc = DxfDocument.Load(loadStream);
                }
                catch (Exception ex) when (version < DxfVersion.AutoCad2000)
                {
                    doc = null;
                    warnings.Add(
                        $"Header upgrade could not be parsed by netDxf for '{fileName}' ({ex.Message}). " +
                        "Attempting legacy ASCII fallback parser.");
                }

                if (doc is null)
                {
                    if (version < DxfVersion.AutoCad2000)
                    {
                        loadStream.Position = 0;
                        using var fallbackReader = new StreamReader(
                            loadStream,
                            Encoding.UTF8,
                            detectEncodingFromByteOrderMarks: true,
                            bufferSize: 1024,
                            leaveOpen: true);
                        var fallbackText = fallbackReader.ReadToEnd();
                        var fallbackPolylines = ParseLegacyAsciiPolylines(fallbackText);
                        if (fallbackPolylines.Count > 0)
                        {
                            warnings.Add(
                                $"Imported '{fileName}' via legacy ASCII fallback parser ({FormatVersion(version)}).");
                            var legacyLayers = ParseLegacyAsciiLayers(fallbackText);
                            var legacyGeom = fallbackPolylines
                                .Select(p => new ImportedGeom { Points = p, ColorHex = null })
                                .ToList();
                            return BuildImportFromGeometry(fileName, legacyGeom, warnings, legacyLayers);
                        }

                        return ImportResult.Fail(
                            $"'{fileName}' is AutoCAD {FormatVersion(version)}. " +
                            "Version header was upgraded for import, but parsing still failed. " +
                            "Re-save as AutoCAD 2000+ ASCII DXF from your CAD tool.",
                            warnings);
                    }

                    return ImportResult.Fail($"Failed to parse '{fileName}'.", warnings);
                }

                var entities = CollectEntities(doc);
                var geometry = new List<ImportedGeom>();
                var layerTallies = new Dictionary<string, (string Name, string ColorHex, int Count)>(
                    StringComparer.OrdinalIgnoreCase);

                SeedLayersFromDocument(doc, layerTallies);

                foreach (var entity in entities)
                {
                    try
                    {
                        TallyEntityEffectiveColor(entity, layerTallies);
                        var batch = new List<List<Point2>>();
                        AppendEntity(entity, batch);
                        var hex = EffectiveColorHex(entity);
                        foreach (var poly in batch)
                        {
                            geometry.Add(new ImportedGeom { Points = poly, ColorHex = hex });
                        }
                    }
                    catch
                    {
                        warnings.Add($"Skipped unsupported entity in '{fileName}'.");
                    }
                }

                var importedLayers = layerTallies.Values
                    // Skip unused table layers (e.g. default Layer 0) so they do not
                    // pollute the workspace palette when no entity uses that color.
                    .Where(v => v.Count > 0)
                    .Select(v => new ImportedLayerInfo
                    {
                        Name = v.Name,
                        ColorHex = v.ColorHex,
                        EntityCount = v.Count,
                    })
                    .OrderByDescending(l => l.EntityCount)
                    .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return BuildImportFromGeometry(fileName, geometry, warnings, importedLayers);
            }
        }
        catch (Exception ex)
        {
            return ImportResult.Fail($"Failed to parse '{fileName}': {ex.Message}", warnings);
        }
    }

    private sealed class ImportedGeom
    {
        public required List<Point2> Points { get; init; }
        public string? ColorHex { get; init; }
    }

    private static ImportResult BuildImportFromGeometry(
        string fileName,
        IReadOnlyList<ImportedGeom> geometry,
        List<string> warnings,
        IReadOnlyList<ImportedLayerInfo>? importedLayers = null)
    {
        if (geometry.Count == 0)
        {
            return ImportResult.Fail($"No drawable geometry found in '{fileName}'.", warnings);
        }

        var allPoints = geometry.SelectMany(g => g.Points);
        var bounds = Bounds2.FromPoints(allPoints);
        var center = bounds.Center;

        var entities = geometry
            .Select(g => new PartEntity
            {
                Polyline = g.Points
                    .Select(p => new Point2(p.X - center.X, p.Y - center.Y))
                    .ToList(),
                SourceColorHex = g.ColorHex is null
                    ? null
                    : LayerPalette.NormalizeHex(g.ColorHex),
            })
            .ToList();

        var localBounds = new Bounds2(
            bounds.MinX - center.X,
            bounds.MinY - center.Y,
            bounds.MaxX - center.X,
            bounds.MaxY - center.Y);

        var part = new PlacedPart
        {
            Name = fileName,
            Entities = entities,
            LocalBounds = localBounds,
            OffsetX = center.X,
            OffsetY = center.Y,
        };

        return ImportResult.Ok(part, warnings, importedLayers);
    }

    private static void SeedLayersFromDocument(
        DxfDocument doc,
        Dictionary<string, (string Name, string ColorHex, int Count)> tallies)
    {
        foreach (var layer in doc.Layers.Items)
        {
            if (layer?.Color is null)
            {
                continue;
            }

            var hex = ToColorHex(layer.Color);
            var name = string.IsNullOrWhiteSpace(layer.Name) ? "Layer" : layer.Name;
            EnsureTally(tallies, name, hex, addCount: 0);
        }
    }

    private static void TallyEntityEffectiveColor(
        EntityObject entity,
        Dictionary<string, (string Name, string ColorHex, int Count)> tallies)
    {
        var hex = EffectiveColorHex(entity);
        if (hex is null)
        {
            return;
        }

        var name = !string.IsNullOrWhiteSpace(entity.Layer?.Name)
            ? entity.Layer!.Name
            : "Layer";
        EnsureTally(tallies, name, hex, addCount: 1);
    }

    private static string? EffectiveColorHex(EntityObject entity)
    {
        var color = entity.Color;
        if (color is null || color.IsByLayer)
        {
            return entity.Layer?.Color is null ? null : ToColorHex(entity.Layer.Color);
        }

        if (color.IsByBlock)
        {
            return entity.Layer?.Color is null ? null : ToColorHex(entity.Layer.Color);
        }

        return ToColorHex(color);
    }

    private static void EnsureTally(
        Dictionary<string, (string Name, string ColorHex, int Count)> tallies,
        string name,
        string colorHex,
        int addCount)
    {
        var key = colorHex;
        if (tallies.TryGetValue(key, out var existing))
        {
            tallies[key] = (existing.Name, existing.ColorHex, existing.Count + addCount);
        }
        else
        {
            tallies[key] = (name, colorHex, addCount);
        }
    }

    private static string ToColorHex(AciColor color) =>
        LayerPalette.FromRgb(color.R, color.G, color.B);

    private static MemoryStream UpgradeAcadVersionTo2000(
        Stream source,
        DxfVersion version,
        string fileName,
        List<string> warnings)
    {
        source.Position = 0;
        string text;
        using (var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            text = reader.ReadToEnd();
        }

        var upgraded = RewriteAcadVerHeader(text);
        if (ReferenceEquals(upgraded, text) || upgraded == text)
        {
            // Still attempt load after a forced replace of common codes if $ACADVER block was odd.
            upgraded = ForceReplaceKnownAcadCodes(text);
        }

        if (upgraded == text && !text.Contains("AC1015", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(
                $"Could not locate $ACADVER in '{fileName}' (reported {FormatVersion(version)}); attempting load anyway.");
        }
        else
        {
            warnings.Add(
                $"Upgraded '{fileName}' from AutoCAD {FormatVersion(version)} to AC1015 for import.");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(upgraded));
    }

    /// <summary>
    /// Rewrites the group-1 value that follows $ACADVER to AC1015 (AutoCAD 2000).
    /// </summary>
    private static string RewriteAcadVerHeader(string dxfText)
    {
        var lines = dxfText.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var changed = false;
        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (!lines[i].Trim().Equals("$ACADVER", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // DXF pairs: code line then value line. Value for $ACADVER is typically under code 1.
            // Walk forward a couple of lines to find the AC#### value.
            for (var j = i + 1; j < Math.Min(i + 4, lines.Length); j++)
            {
                var candidate = lines[j].Trim();
                if (AcadVerValue.IsMatch(candidate))
                {
                    lines[j] = lines[j].Replace(candidate, "AC1015", StringComparison.OrdinalIgnoreCase);
                    changed = true;
                    break;
                }
            }

            break;
        }

        if (!changed)
        {
            return dxfText;
        }

        var newline = dxfText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return string.Join(newline, lines);
    }

    private static string ForceReplaceKnownAcadCodes(string dxfText)
    {
        // Last-resort for odd formatting: replace known pre-2000 codes once.
        string[] oldCodes =
        [
            "AC1014", "AC1012", "AC1011", "AC1009", "AC1006", "AC1004", "AC1003", "AC1002", "AC1001",
        ];

        foreach (var code in oldCodes)
        {
            if (dxfText.Contains(code, StringComparison.OrdinalIgnoreCase))
            {
                return Regex.Replace(
                    dxfText,
                    code,
                    "AC1015",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
        }

        return dxfText;
    }

    private static List<List<Point2>> ParseLegacyAsciiPolylines(string text)
    {
        var pairs = ReadDxfPairs(text);
        var polylines = new List<List<Point2>>();

        var inEntities = false;
        var currentEntity = string.Empty;
        var entity = new Dictionary<int, string>();
        List<(Point2 Point, double Bulge)>? polylineVertices = null;
        var polylineClosed = false;
        Dictionary<int, string>? vertexEntity = null;
        double? pendingX = null;

        static void FinalizeEntity(
            string entityName,
            Dictionary<int, string> data,
            List<(Point2 Point, double Bulge)>? lwVertices,
            bool lwClosed,
            List<List<Point2>> outPolylines)
        {
            if (entityName.Equals("LWPOLYLINE", StringComparison.OrdinalIgnoreCase))
            {
                if (lwVertices is { Count: > 0 })
                {
                    FinalizeLegacyPolyline(lwVertices, lwClosed, outPolylines);
                }

                return;
            }

            switch (entityName)
            {
                case "LINE":
                {
                    if (TryGetDouble(data, 10, out var x1) &&
                        TryGetDouble(data, 20, out var y1) &&
                        TryGetDouble(data, 11, out var x2) &&
                        TryGetDouble(data, 21, out var y2))
                    {
                        outPolylines.Add([new Point2(x1, y1), new Point2(x2, y2)]);
                    }
                    break;
                }
                case "CIRCLE":
                {
                    if (TryGetDouble(data, 10, out var cx) &&
                        TryGetDouble(data, 20, out var cy) &&
                        TryGetDouble(data, 40, out var r))
                    {
                        outPolylines.Add(TessellateArc(cx, cy, r, 0, 360, closed: true));
                    }
                    break;
                }
                case "ARC":
                {
                    if (TryGetDouble(data, 10, out var cx) &&
                        TryGetDouble(data, 20, out var cy) &&
                        TryGetDouble(data, 40, out var r) &&
                        TryGetDouble(data, 50, out var a0) &&
                        TryGetDouble(data, 51, out var a1))
                    {
                        outPolylines.Add(TessellateArc(cx, cy, r, a0, a1, closed: false));
                    }
                    break;
                }
            }
        }

        static void FinalizeLegacyPolyline(
            List<(Point2 Point, double Bulge)> vertices,
            bool isClosed,
            List<List<Point2>> outPolylines)
        {
            if (vertices.Count < 2)
            {
                return;
            }

            var points = new List<Point2>();
            for (var i = 0; i < vertices.Count; i++)
            {
                var next = i + 1;
                if (next >= vertices.Count)
                {
                    if (!isClosed)
                    {
                        break;
                    }
                    next = 0;
                }

                var current = vertices[i];
                var end = vertices[next];
                if (points.Count == 0)
                {
                    points.Add(current.Point);
                }

                if (Math.Abs(current.Bulge) < 1e-12)
                {
                    points.Add(end.Point);
                }
                else
                {
                    points.AddRange(TessellateBulge(current.Point, end.Point, current.Bulge).Skip(1));
                }
            }

            if (points.Count >= 2)
            {
                outPolylines.Add(points);
            }
        }

        void FlushCurrentEntity()
        {
            if (string.IsNullOrEmpty(currentEntity))
            {
                return;
            }

            if (currentEntity.Equals("POLYLINE", StringComparison.OrdinalIgnoreCase) ||
                currentEntity.Equals("VERTEX", StringComparison.OrdinalIgnoreCase) ||
                currentEntity.Equals("SEQEND", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            FinalizeEntity(currentEntity, entity, polylineVertices, polylineClosed, polylines);
            entity = new Dictionary<int, string>();
            if (currentEntity.Equals("LWPOLYLINE", StringComparison.OrdinalIgnoreCase))
            {
                polylineVertices = null;
                polylineClosed = false;
            }
        }

        foreach (var (code, value) in pairs)
        {
            if (code == 0 && value.Equals("SECTION", StringComparison.OrdinalIgnoreCase))
            {
                inEntities = false;
                continue;
            }

            if (code == 2 && value.Equals("ENTITIES", StringComparison.OrdinalIgnoreCase))
            {
                inEntities = true;
                continue;
            }

            if (!inEntities)
            {
                continue;
            }

            if (code == 0 && value.Equals("ENDSEC", StringComparison.OrdinalIgnoreCase))
            {
                FlushCurrentEntity();
                if (polylineVertices is { Count: > 0 } &&
                    currentEntity.Equals("POLYLINE", StringComparison.OrdinalIgnoreCase))
                {
                    FinalizeLegacyPolyline(polylineVertices, polylineClosed, polylines);
                }
                break;
            }

            if (code == 0)
            {
                FlushCurrentEntity();

                if (value.Equals("POLYLINE", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("LWPOLYLINE", StringComparison.OrdinalIgnoreCase))
                {
                    polylineVertices = [];
                    polylineClosed = false;
                    vertexEntity = null;
                    pendingX = null;
                    currentEntity = value;
                    continue;
                }

                if (value.Equals("VERTEX", StringComparison.OrdinalIgnoreCase))
                {
                    currentEntity = value;
                    vertexEntity = [];
                    continue;
                }

                if (value.Equals("SEQEND", StringComparison.OrdinalIgnoreCase))
                {
                    if (polylineVertices is { Count: > 0 })
                    {
                        FinalizeLegacyPolyline(polylineVertices, polylineClosed, polylines);
                    }
                    polylineVertices = null;
                    vertexEntity = null;
                    pendingX = null;
                    currentEntity = string.Empty;
                    continue;
                }

                currentEntity = value;
                pendingX = null;
                continue;
            }

            if (currentEntity.Equals("POLYLINE", StringComparison.OrdinalIgnoreCase))
            {
                entity[code] = value;
                if (code == 70 && int.TryParse(value, out var flags))
                {
                    polylineClosed = (flags & 1) != 0;
                }
                continue;
            }

            if (currentEntity.Equals("LWPOLYLINE", StringComparison.OrdinalIgnoreCase))
            {
                polylineVertices ??= [];
                if (code == 70 && int.TryParse(value, out var flags))
                {
                    polylineClosed = (flags & 1) != 0;
                }
                else if (code == 10 &&
                         double.TryParse(value, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out var x))
                {
                    pendingX = x;
                }
                else if (code == 20 &&
                         pendingX is double px &&
                         double.TryParse(value, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out var y))
                {
                    polylineVertices.Add((new Point2(px, y), 0));
                    pendingX = null;
                }
                else if (code == 42 &&
                         polylineVertices.Count > 0 &&
                         double.TryParse(value, System.Globalization.NumberStyles.Float,
                             System.Globalization.CultureInfo.InvariantCulture, out var bulge))
                {
                    var last = polylineVertices[^1];
                    polylineVertices[^1] = (last.Point, bulge);
                }

                continue;
            }

            if (currentEntity.Equals("VERTEX", StringComparison.OrdinalIgnoreCase))
            {
                vertexEntity ??= [];
                vertexEntity[code] = value;
                if (code == 42 || code == 20)
                {
                    if (TryGetDouble(vertexEntity, 10, out var vx) &&
                        TryGetDouble(vertexEntity, 20, out var vy))
                    {
                        var bulge = TryGetDouble(vertexEntity, 42, out var b) ? b : 0;
                        if (polylineVertices is not null)
                        {
                            var isNew = polylineVertices.Count == 0 ||
                                        Math.Abs(polylineVertices[^1].Point.X - vx) > 1e-9 ||
                                        Math.Abs(polylineVertices[^1].Point.Y - vy) > 1e-9;
                            if (isNew)
                            {
                                polylineVertices.Add((new Point2(vx, vy), bulge));
                            }
                            else
                            {
                                var last = polylineVertices[^1];
                                polylineVertices[^1] = (last.Point, bulge);
                            }
                        }
                    }
                }
                continue;
            }

            entity[code] = value;
        }

        return polylines;
    }

    /// <summary>
    /// Parses LAYER table records from ASCII DXF text (name + ACI/true color).
    /// </summary>
    private static List<ImportedLayerInfo> ParseLegacyAsciiLayers(string text)
    {
        var pairs = ReadDxfPairs(text);
        var tallies = new Dictionary<string, (string Name, string ColorHex, int Count)>(
            StringComparer.OrdinalIgnoreCase);

        var inTables = false;
        var inLayerTable = false;
        string? pendingName = null;
        short? pendingAci = null;
        int? pendingTrueColor = null;

        void FlushLayer()
        {
            if (string.IsNullOrWhiteSpace(pendingName))
            {
                pendingName = null;
                pendingAci = null;
                pendingTrueColor = null;
                return;
            }

            string hex;
            if (pendingTrueColor is int tc)
            {
                var aci = AciColor.FromTrueColor(tc);
                hex = ToColorHex(aci);
            }
            else if (pendingAci is short idx && idx >= 1 && idx <= 255)
            {
                hex = ToColorHex(AciColor.FromCadIndex(idx));
            }
            else
            {
                // Default ACI 7 (white) when color omitted.
                hex = ToColorHex(AciColor.FromCadIndex(7));
            }

            EnsureTally(tallies, pendingName.Trim(), hex, addCount: 0);
            pendingName = null;
            pendingAci = null;
            pendingTrueColor = null;
        }

        for (var i = 0; i < pairs.Count; i++)
        {
            var (code, value) = pairs[i];
            var trimmed = value.Trim();

            if (code == 0 && trimmed.Equals("SECTION", StringComparison.OrdinalIgnoreCase))
            {
                // Look ahead for section name
                continue;
            }

            if (code == 2 && trimmed.Equals("TABLES", StringComparison.OrdinalIgnoreCase))
            {
                inTables = true;
                continue;
            }

            if (code == 0 && trimmed.Equals("ENDSEC", StringComparison.OrdinalIgnoreCase))
            {
                if (inLayerTable)
                {
                    FlushLayer();
                }

                inTables = false;
                inLayerTable = false;
                continue;
            }

            if (!inTables)
            {
                continue;
            }

            if (code == 0 && trimmed.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
            {
                FlushLayer();
                inLayerTable = false;
                continue;
            }

            if (code == 2 && inTables && !inLayerTable &&
                trimmed.Equals("LAYER", StringComparison.OrdinalIgnoreCase))
            {
                // Start of LAYER table (table name), not a layer record.
                inLayerTable = true;
                continue;
            }

            if (code == 0 && trimmed.Equals("ENDTAB", StringComparison.OrdinalIgnoreCase))
            {
                FlushLayer();
                inLayerTable = false;
                continue;
            }

            if (!inLayerTable)
            {
                continue;
            }

            if (code == 0 && trimmed.Equals("LAYER", StringComparison.OrdinalIgnoreCase))
            {
                FlushLayer();
                continue;
            }

            if (code == 2 && pendingName is null)
            {
                pendingName = trimmed;
            }
            else if (code == 62 && short.TryParse(trimmed, out var aci))
            {
                // Negative ACI means layer is off; use absolute index for color.
                var abs = Math.Abs(aci);
                pendingAci = abs is >= 1 and <= 255 ? (short)abs : aci;
            }
            else if (code == 420 && int.TryParse(trimmed, out var trueColor))
            {
                pendingTrueColor = trueColor;
            }
        }

        FlushLayer();

        return tallies.Values
            // Legacy geometry has no per-entity colors, so keep table layers even when Count is 0.
            .Select(v => new ImportedLayerInfo
            {
                Name = v.Name,
                ColorHex = v.ColorHex,
                EntityCount = v.Count,
            })
            .OrderByDescending(l => l.EntityCount)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<(int Code, string Value)> ReadDxfPairs(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var pairs = new List<(int Code, string Value)>(lines.Length / 2);
        for (var i = 0; i + 1 < lines.Length; i += 2)
        {
            if (int.TryParse(lines[i].Trim(), out var code))
            {
                pairs.Add((code, lines[i + 1].Trim()));
            }
        }
        return pairs;
    }

    private static bool TryGetDouble(IReadOnlyDictionary<int, string> map, int code, out double value)
    {
        if (map.TryGetValue(code, out var raw) &&
            double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static string FormatVersion(DxfVersion version) => version switch
    {
        DxfVersion.AutoCad14 => "R14",
        DxfVersion.AutoCad13 => "R13",
        DxfVersion.AutoCad12 => "R12",
        DxfVersion.AutoCad10 => "R10",
        DxfVersion.AutoCad9 => "R9",
        _ => version.ToString(),
    };

    private static List<EntityObject> CollectEntities(DxfDocument doc)
    {
        var result = new List<EntityObject>();
        foreach (var entity in doc.Entities.All)
        {
            if (entity is Insert insert)
            {
                result.AddRange(insert.Explode());
            }
            else
            {
                result.Add(entity);
            }
        }

        // Explode nested inserts once more if needed.
        for (var i = 0; i < result.Count; i++)
        {
            if (result[i] is Insert nested)
            {
                var exploded = nested.Explode();
                result.RemoveAt(i);
                result.InsertRange(i, exploded);
                i--;
            }
        }

        return result;
    }

    private static void AppendEntity(EntityObject entity, List<List<Point2>> polylines)
    {
        switch (entity)
        {
            case Line line:
                polylines.Add(
                [
                    new Point2(line.StartPoint.X, line.StartPoint.Y),
                    new Point2(line.EndPoint.X, line.EndPoint.Y),
                ]);
                break;

            case Circle circle:
                polylines.Add(TessellateArc(
                    circle.Center.X, circle.Center.Y, circle.Radius, 0, 360, closed: true));
                break;

            case Arc arc:
                polylines.Add(TessellateArc(
                    arc.Center.X, arc.Center.Y, arc.Radius, arc.StartAngle, arc.EndAngle, closed: false));
                break;

            case Polyline2D poly2D:
                AppendPolyline2D(poly2D, polylines);
                break;

            case Polyline3D poly3D:
                var pts3 = poly3D.Vertexes.Select(v => new Point2(v.X, v.Y)).ToList();
                if (poly3D.IsClosed && pts3.Count > 1)
                {
                    pts3.Add(pts3[0]);
                }

                if (pts3.Count >= 2)
                {
                    polylines.Add(pts3);
                }

                break;

            case Ellipse ellipse:
                AppendEllipse(ellipse, polylines);
                break;
        }
    }

    private static void AppendPolyline2D(Polyline2D poly, List<List<Point2>> polylines)
    {
        var verts = poly.Vertexes;
        if (verts.Count < 2)
        {
            return;
        }

        var points = new List<Point2>();
        for (var i = 0; i < verts.Count; i++)
        {
            var current = verts[i];
            var next = verts[(i + 1) % verts.Count];
            var start = new Point2(current.Position.X, current.Position.Y);

            if (points.Count == 0)
            {
                points.Add(start);
            }

            var isLast = i == verts.Count - 1;
            if (isLast && !poly.IsClosed)
            {
                break;
            }

            if (Math.Abs(current.Bulge) < 1e-12)
            {
                points.Add(new Point2(next.Position.X, next.Position.Y));
            }
            else
            {
                var bulgePoints = TessellateBulge(
                    start,
                    new Point2(next.Position.X, next.Position.Y),
                    current.Bulge);
                // Skip first point (already added).
                points.AddRange(bulgePoints.Skip(1));
            }

            if (isLast)
            {
                break;
            }
        }

        if (points.Count >= 2)
        {
            polylines.Add(points);
        }
    }

    private static void AppendEllipse(Ellipse ellipse, List<List<Point2>> polylines)
    {
        var start = ellipse.StartAngle;
        var end = ellipse.EndAngle;
        var closed = Math.Abs(end - start) < 1e-6 || Math.Abs(Math.Abs(end - start) - 360) < 1e-6;
        if (closed)
        {
            start = 0;
            end = 360;
        }

        var major = ellipse.MajorAxis;
        var minor = ellipse.MinorAxis;
        var cx = ellipse.Center.X;
        var cy = ellipse.Center.Y;
        var rotation = ellipse.Rotation * Math.PI / 180.0;
        var cosR = Math.Cos(rotation);
        var sinR = Math.Sin(rotation);

        var span = end - start;
        if (span < 0)
        {
            span += 360;
        }

        var steps = Math.Max(24, (int)Math.Ceiling(ArcSegments * span / 360.0));
        var points = new List<Point2>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var t = start + span * i / steps;
            var rad = t * Math.PI / 180.0;
            var lx = major * 0.5 * Math.Cos(rad);
            var ly = minor * 0.5 * Math.Sin(rad);
            var x = cx + lx * cosR - ly * sinR;
            var y = cy + lx * sinR + ly * cosR;
            points.Add(new Point2(x, y));
        }

        polylines.Add(points);
    }

    private static List<Point2> TessellateArc(
        double cx, double cy, double radius, double startDeg, double endDeg, bool closed)
    {
        var start = startDeg;
        var end = endDeg;
        var span = end - start;
        if (closed)
        {
            span = 360;
            start = 0;
        }
        else if (span < 0)
        {
            span += 360;
        }

        var steps = Math.Max(8, (int)Math.Ceiling(ArcSegments * span / 360.0));
        var points = new List<Point2>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var a = (start + span * i / steps) * Math.PI / 180.0;
            points.Add(new Point2(cx + radius * Math.Cos(a), cy + radius * Math.Sin(a)));
        }

        return points;
    }

    private static List<Point2> TessellateBulge(Point2 start, Point2 end, double bulge)
    {
        // Bulge = tan(theta/4); positive = CCW.
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord < 1e-12 || Math.Abs(bulge) < 1e-12)
        {
            return [start, end];
        }

        var angle = 4 * Math.Atan(bulge);
        var radius = chord / (2 * Math.Sin(Math.Abs(angle) * 0.5));
        var midX = (start.X + end.X) * 0.5;
        var midY = (start.Y + end.Y) * 0.5;
        var cx = midX - dy * (1 - bulge * bulge) / (4 * bulge);
        var cy = midY + dx * (1 - bulge * bulge) / (4 * bulge);

        var a0 = Math.Atan2(start.Y - cy, start.X - cx);
        var a1 = Math.Atan2(end.Y - cy, end.X - cx);
        var sweep = a1 - a0;
        if (bulge > 0 && sweep < 0)
        {
            sweep += 2 * Math.PI;
        }
        else if (bulge < 0 && sweep > 0)
        {
            sweep -= 2 * Math.PI;
        }

        var steps = Math.Max(4, (int)Math.Ceiling(ArcSegments * Math.Abs(sweep) / (2 * Math.PI)));
        var points = new List<Point2>(steps + 1) { start };
        for (var i = 1; i < steps; i++)
        {
            var a = a0 + sweep * i / steps;
            points.Add(new Point2(cx + radius * Math.Cos(a), cy + radius * Math.Sin(a)));
        }

        points.Add(end);
        return points;
    }
}

public sealed class ImportResult
{
    public bool Success { get; init; }
    public PlacedPart? Part { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<ImportedLayerInfo> Layers { get; init; } = [];

    public static ImportResult Ok(
        PlacedPart part,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<ImportedLayerInfo>? layers = null) => new()
    {
        Success = true,
        Part = part,
        Warnings = warnings ?? [],
        Layers = layers ?? [],
    };

    public static ImportResult Fail(string error, IReadOnlyList<string>? warnings = null) => new()
    {
        Success = false,
        Error = error,
        Warnings = warnings ?? [],
    };
}

