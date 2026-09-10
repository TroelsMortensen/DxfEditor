namespace BlazorWasmUI.Models;

public sealed class PartEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Polyline in part-local space (origin at parent local bbox center).</summary>
    public required IReadOnlyList<Point2> Polyline { get; init; }

    /// <summary>Workspace layer this entity is assigned to (ByLayer color).</summary>
    public Guid? LayerId { get; set; }

    /// <summary>Source DXF color hex used during import before LayerId is resolved.</summary>
    public string? SourceColorHex { get; set; }
}
