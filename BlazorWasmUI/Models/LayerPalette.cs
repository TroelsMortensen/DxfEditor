namespace BlazorWasmUI.Models;

/// <summary>Fixed set of distinct colors offered when manually adding a layer.</summary>
public static class LayerPalette
{
    public static IReadOnlyList<string> Colors { get; } =
    [
        "#E53935", // red
        "#FB8C00", // orange
        "#FDD835", // yellow
        "#43A047", // green
        "#00ACC1", // cyan
        "#1E88E5", // blue
        "#8E24AA", // purple
        "#D81B60", // magenta
        "#6D4C41", // brown
        "#ECEFF1", // light gray / white
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
}
