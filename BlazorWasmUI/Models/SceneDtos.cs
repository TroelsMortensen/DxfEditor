namespace BlazorWasmUI.Models;

public sealed class SceneDto
{
    public List<ScenePartDto> Parts { get; set; } = [];
    public List<string> SelectedPartIds { get; set; } = [];
    public List<string> SelectedEntityIds { get; set; } = [];
    public ViewportDto Viewport { get; set; } = new();
}

public sealed class ScenePartDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<SceneEntityDto> Entities { get; set; } = [];
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double RotationDegrees { get; set; }
    public bool Mirrored { get; set; }
    public double LocalMinX { get; set; }
    public double LocalMinY { get; set; }
    public double LocalMaxX { get; set; }
    public double LocalMaxY { get; set; }
}

public sealed class SceneEntityDto
{
    public string Id { get; set; } = "";
    public double[] Polyline { get; set; } = [];
    public string? ColorHex { get; set; }
}

public sealed class ViewportDto
{
    public double PanX { get; set; }
    public double PanY { get; set; }
    public double Zoom { get; set; } = 1;
}

public sealed class PartTransformDto
{
    public string Id { get; set; } = "";
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double RotationDegrees { get; set; }
    public bool Mirrored { get; set; }
}
