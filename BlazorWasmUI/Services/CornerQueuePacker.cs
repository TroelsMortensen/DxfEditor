using BlazorWasmUI.Models;

namespace BlazorWasmUI.Services;

/// <summary>
/// Omnidirectional corner-queue / bottom-left-fill packer for axis-aligned rectangles.
/// Outputs bottom-left placements in the same order as the input sizes.
/// </summary>
public static class CornerQueuePacker
{
    public readonly record struct Size(double Width, double Height);

    public readonly record struct Placement(double BottomLeftX, double BottomLeftY, double Width, double Height);

    /// <param name="sizes">Part sizes in input order.</param>
    /// <param name="obstacles">Existing world AABBs that must not be overlapped.</param>
    /// <param name="origin">Distance-sort origin for candidate ranking.</param>
    /// <param name="spacing">Gap between placed rects.</param>
    /// <param name="firstCenter">
    /// When set and there are no obstacles, the first (largest after sort) piece is centered here.
    /// Ignored when obstacles are present (all pieces use the candidate queue).
    /// </param>
    public static Placement[] Pack(
        IReadOnlyList<Size> sizes,
        IReadOnlyList<Bounds2> obstacles,
        Point2 origin,
        double spacing = 20,
        Point2? firstCenter = null)
    {
        var n = sizes.Count;
        var result = new Placement[n];
        if (n == 0)
        {
            return result;
        }

        var order = Enumerable.Range(0, n)
            .OrderByDescending(i => Math.Max(sizes[i].Width, sizes[i].Height))
            .ThenByDescending(i => sizes[i].Width * sizes[i].Height)
            .ThenByDescending(i => Math.Abs(sizes[i].Width - sizes[i].Height))
            .ThenBy(i => i)
            .ToList();

        var occupied = new List<Bounds2>(obstacles);
        var persistent = new SortedSet<Candidate>(CandidateComparer.Instance);

        if (obstacles.Count > 0)
        {
            foreach (var o in obstacles)
            {
                SeedAnchorsFromBounds(persistent, o, spacing, origin);
            }
        }

        var placeFirstAtCenter = firstCenter is { } && obstacles.Count == 0;
        var firstPlaced = false;

        foreach (var index in order)
        {
            var w = Math.Max(sizes[index].Width, 1);
            var h = Math.Max(sizes[index].Height, 1);

            double blX;
            double blY;

            if (placeFirstAtCenter && !firstPlaced)
            {
                var c = firstCenter!.Value;
                blX = c.X - w * 0.5;
                blY = c.Y - h * 0.5;
                firstPlaced = true;
            }
            else
            {
                var chosen = TryPlace(w, h, occupied, persistent, origin, spacing);
                blX = chosen.X;
                blY = chosen.Y;
            }

            var placed = new Bounds2(blX, blY, blX + w, blY + h);
            occupied.Add(placed);
            result[index] = new Placement(blX, blY, w, h);

            // Consume exact BL match from persistent set if present.
            persistent.Remove(new Candidate(blX, blY, origin));
            SeedAnchorsFromBounds(persistent, placed, spacing, origin);
        }

        return result;
    }

    private static Point2 TryPlace(
        double w,
        double h,
        List<Bounds2> occupied,
        SortedSet<Candidate> persistent,
        Point2 origin,
        double spacing)
    {
        var trials = new SortedSet<Candidate>(CandidateComparer.Instance);

        foreach (var c in persistent)
        {
            trials.Add(c);
        }

        foreach (var o in occupied)
        {
            AddTrial(trials, o.MaxX + spacing, o.MinY, origin);
            AddTrial(trials, o.MinX - w - spacing, o.MinY, origin);
            AddTrial(trials, o.MinX, o.MaxY + spacing, origin);
            AddTrial(trials, o.MinX, o.MinY - h - spacing, origin);
            AddTrial(trials, o.MaxX + spacing, o.MaxY + spacing, origin);
            AddTrial(trials, o.MinX - w - spacing, o.MaxY + spacing, origin);
            AddTrial(trials, o.MaxX + spacing, o.MinY - h - spacing, origin);
            AddTrial(trials, o.MinX - w - spacing, o.MinY - h - spacing, origin);
        }

        foreach (var trial in trials)
        {
            var proposed = new Bounds2(trial.X, trial.Y, trial.X + w, trial.Y + h);
            if (!Collides(proposed, occupied))
            {
                return new Point2(trial.X, trial.Y);
            }
        }

        return Fallback(occupied, origin, spacing);
    }

    private static Point2 Fallback(List<Bounds2> occupied, Point2 origin, double spacing)
    {
        if (occupied.Count == 0)
        {
            return origin;
        }

        var union = Union(occupied);
        return new Point2(union.MaxX + spacing, union.MinY);
    }

    private static bool Collides(Bounds2 proposed, List<Bounds2> occupied)
    {
        foreach (var o in occupied)
        {
            if (proposed.Intersects(o))
            {
                return true;
            }
        }

        return false;
    }

    private static Bounds2 Union(IReadOnlyList<Bounds2> rects)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var r in rects)
        {
            if (r.MinX < minX) minX = r.MinX;
            if (r.MinY < minY) minY = r.MinY;
            if (r.MaxX > maxX) maxX = r.MaxX;
            if (r.MaxY > maxY) maxY = r.MaxY;
        }

        return new Bounds2(minX, minY, maxX, maxY);
    }

    private static void SeedAnchorsFromBounds(
        SortedSet<Candidate> persistent,
        Bounds2 b,
        double spacing,
        Point2 origin)
    {
        // Size-independent east / north / NE attachment points (BL candidates).
        AddTrial(persistent, b.MaxX + spacing, b.MinY, origin);
        AddTrial(persistent, b.MinX, b.MaxY + spacing, origin);
        AddTrial(persistent, b.MaxX + spacing, b.MaxY + spacing, origin);
    }

    private static void AddTrial(SortedSet<Candidate> set, double x, double y, Point2 origin) =>
        set.Add(new Candidate(x, y, origin));

    private readonly struct Candidate
    {
        public Candidate(double x, double y, Point2 origin)
        {
            X = x;
            Y = y;
            var dx = x - origin.X;
            var dy = y - origin.Y;
            DistSq = dx * dx + dy * dy;
        }

        public double X { get; }
        public double Y { get; }
        public double DistSq { get; }
    }

    private sealed class CandidateComparer : IComparer<Candidate>
    {
        public static readonly CandidateComparer Instance = new();

        public int Compare(Candidate a, Candidate b)
        {
            var d = a.DistSq.CompareTo(b.DistSq);
            if (d != 0) return d;
            d = a.X.CompareTo(b.X);
            if (d != 0) return d;
            return a.Y.CompareTo(b.Y);
        }
    }
}
