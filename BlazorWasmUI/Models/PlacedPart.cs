namespace BlazorWasmUI.Models;

public sealed class PlacedPart
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }

    /// <summary>Polylines in part-local space (origin at local bbox center).</summary>
    public required IReadOnlyList<IReadOnlyList<Point2>> LocalPolylines { get; init; }

    public required Bounds2 LocalBounds { get; init; }

    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double RotationDegrees { get; set; }
    public bool Mirrored { get; set; }

    public Point2 TransformLocalToWorld(Point2 local)
    {
        var x = local.X;
        var y = local.Y;
        if (Mirrored)
        {
            x = -x;
        }

        var rad = RotationDegrees * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        var rx = x * cos - y * sin;
        var ry = x * sin + y * cos;
        return new Point2(rx + OffsetX, ry + OffsetY);
    }

    public Bounds2 GetWorldBounds()
    {
        var corners = new[]
        {
            TransformLocalToWorld(new Point2(LocalBounds.MinX, LocalBounds.MinY)),
            TransformLocalToWorld(new Point2(LocalBounds.MaxX, LocalBounds.MinY)),
            TransformLocalToWorld(new Point2(LocalBounds.MaxX, LocalBounds.MaxY)),
            TransformLocalToWorld(new Point2(LocalBounds.MinX, LocalBounds.MaxY)),
        };
        return Bounds2.FromPoints(corners);
    }
}
