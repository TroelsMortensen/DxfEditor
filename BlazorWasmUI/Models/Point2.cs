namespace BlazorWasmUI.Models;

public readonly record struct Point2(double X, double Y)
{
    public static Point2 operator +(Point2 a, Point2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Point2 operator -(Point2 a, Point2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2 operator *(Point2 p, double s) => new(p.X * s, p.Y * s);

    public double LengthSquared => X * X + Y * Y;
}

public readonly record struct Bounds2(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public Point2 Center => new((MinX + MaxX) * 0.5, (MinY + MaxY) * 0.5);

    public static Bounds2 FromPoints(IEnumerable<Point2> points)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var any = false;

        foreach (var p in points)
        {
            any = true;
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        return any ? new Bounds2(minX, minY, maxX, maxY) : new Bounds2(0, 0, 0, 0);
    }

    public Bounds2 Inflate(double amount) =>
        new(MinX - amount, MinY - amount, MaxX + amount, MaxY + amount);

    public bool Intersects(Bounds2 other) =>
        MinX <= other.MaxX && MaxX >= other.MinX && MinY <= other.MaxY && MaxY >= other.MinY;

    public bool Contains(Point2 p) =>
        p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;
}
