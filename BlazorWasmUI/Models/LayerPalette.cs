using System.Globalization;

namespace BlazorWasmUI.Models;

/// <summary>
/// UI palette (LightBurn C00–C09) plus the full official LightBurn RGB table for export snapping.
/// </summary>
public static class LayerPalette
{
    /// <summary>Colors offered when manually adding a layer (LightBurn C00–C09).</summary>
    public static IReadOnlyList<string> Colors { get; } =
    [
        "#000000", // C00
        "#0000FF", // C01
        "#FF0000", // C02
        "#00E000", // C03
        "#D0D000", // C04
        "#FF8000", // C05
        "#00E0E0", // C06
        "#FF00FF", // C07
        "#B4B4B4", // C08
        "#0000A0", // C09
    ];

    /// <summary>Full LightBurn layer palette (C00–C29 + T1/T2) for nearest-color export snap.</summary>
    public static IReadOnlyList<string> LightBurnColors { get; } =
    [
        "#000000", // C00
        "#0000FF", // C01
        "#FF0000", // C02
        "#00E000", // C03
        "#D0D000", // C04
        "#FF8000", // C05
        "#00E0E0", // C06
        "#FF00FF", // C07
        "#B4B4B4", // C08
        "#0000A0", // C09
        "#A00000", // C10
        "#00A000", // C11
        "#A0A000", // C12
        "#C08000", // C13
        "#00A0FF", // C14
        "#A000A0", // C15
        "#808080", // C16
        "#7D87B9", // C17
        "#BB7784", // C18
        "#4A6FE3", // C19
        "#D33F6A", // C20
        "#8CD78C", // C21
        "#F0B98D", // C22
        "#F6C4E1", // C23
        "#FA9ED4", // C24
        "#500A78", // C25
        "#B45A00", // C26
        "#004754", // C27
        "#86FA88", // C28
        "#FFDB66", // C29
        "#F36926", // T1
        "#0C96D9", // T2
    ];

    public static string NormalizeHex(string colorHex)
    {
        var hex = colorHex.Trim();
        if (!hex.StartsWith('#'))
        {
            hex = "#" + hex;
        }

        return hex.ToUpperInvariant();
    }

    public static string FromRgb(byte r, byte g, byte b) =>
        $"#{r:X2}{g:X2}{b:X2}";

    /// <summary>
    /// Maps any hex to the nearest official LightBurn palette color (Euclidean RGB).
    /// CAD white / near-white maps to C00 black (same rule as LightBurn).
    /// </summary>
    public static string NearestLightBurnHex(string colorHex)
    {
        if (!TryParseRgb(colorHex, out var r, out var g, out var b))
        {
            return LightBurnColors[0];
        }

        // LightBurn treats white as black; Euclidean nearest would pick pale pink (C23).
        if (r >= 250 && g >= 250 && b >= 250)
        {
            return LightBurnColors[0];
        }

        string best = LightBurnColors[0];
        var bestDist = int.MaxValue;

        foreach (var candidate in LightBurnColors)
        {
            if (!TryParseRgb(candidate, out var cr, out var cg, out var cb))
            {
                continue;
            }

            var dr = r - cr;
            var dg = g - cg;
            var db = b - cb;
            var dist = (dr * dr) + (dg * dg) + (db * db);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        return best;
    }

    public static bool TryParseRgb(string colorHex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        var hex = NormalizeHex(colorHex).TrimStart('#');
        if (hex.Length != 6)
        {
            return false;
        }

        return byte.TryParse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
            && byte.TryParse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
            && byte.TryParse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b);
    }
}
