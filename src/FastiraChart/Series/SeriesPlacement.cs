using System.Windows;
using System.Windows.Media;
using FastiraChart.Axes;
using FastiraChart.Gridlines;

namespace FastiraChart.Series;

/// <summary>
/// The scale every series in a control is placed by: the plot rectangle and both axis ranges, with each
/// range's reciprocal computed once.
/// </summary>
/// <param name="Plot">The plot rectangle, in the space the ticks are placed in.</param>
/// <param name="MinimumX">The horizontal axis minimum.</param>
/// <param name="MaximumX">The horizontal axis maximum.</param>
/// <param name="HorizontalInverseRange">The horizontal range's reciprocal, as the tick generator computes it.</param>
/// <param name="MinimumY">The vertical axis minimum.</param>
/// <param name="MaximumY">The vertical axis maximum.</param>
/// <param name="VerticalInverseRange">The vertical range's reciprocal, as the tick generator computes it.</param>
internal readonly record struct SeriesScale(
    Rect Plot,
    double MinimumX,
    double MaximumX,
    double HorizontalInverseRange,
    double MinimumY,
    double MaximumY,
    double VerticalInverseRange);

/// <summary>
/// The geometry one series produced, and what went into it.
/// </summary>
/// <param name="Geometry">Every line the series draws, as one frozen geometry; null when it draws none.</param>
/// <param name="Figures">Unbroken runs of two or more points: one figure each.</param>
/// <param name="Segments">Lines between consecutive placed points, zero-length ones included.</param>
/// <param name="Placed">Points that were placed — every finite point, in range or not.</param>
/// <param name="Gaps">Points that were not placed because a value, or the coordinate it maps to, is not finite.</param>
/// <param name="Trimmed">
/// Segments with an end beyond the render guard, drawn only as far as the guard — or not at all when they lie
/// wholly beyond it. See <see cref="SeriesPlacement.RenderGuardMargin"/>.
/// </param>
internal readonly record struct SeriesGeometry(StreamGeometry? Geometry, int Figures, int Segments, int Placed, int Gaps, int Trimmed)
{
    /// <summary>No geometry at all.</summary>
    public static SeriesGeometry None => default;
}

/// <summary>
/// Turns a series' points and a scale into the geometry that draws it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No scale of its own.</b> A value becomes a normalized position through
/// <see cref="LinearAxisMapping.Position"/> — which, inside the range, is the tick generator's
/// <see cref="LinearAxisMapping.Normalize"/> itself — and the position becomes a coordinate through
/// <see cref="AxisTickPlacement.HorizontalCoordinate(Rect, double)"/> and
/// <see cref="AxisTickPlacement.VerticalCoordinate(Rect, double)"/>, the expressions the tick marks, the grid
/// and the origin and reference lines use. A series point at a value any of them also marks reaches the same
/// double, as one calculation rather than two that agree.
/// </para>
/// <para>
/// <b>Every finite point is placed</b>, including one far outside the range: the segments to it keep their
/// direction, and the clip decides what is seen. A point is a gap only when a value is not finite, or when
/// it lies so far outside that its coordinate overflows. Nothing is sorted, merged or skipped for density.
/// </para>
/// <para>
/// <b>The render guard.</b> WPF does not draw a line whose coordinates are too large: a segment
/// between two points about two hundred thousand DIP off the plot silently missing — the whole segment,
/// including the part that crosses the plot. So a segment with an end beyond <see cref="RenderGuardMargin"/>
/// of the plot is cut, in double precision, where it leaves that band, and only the part inside is handed to
/// WPF. The part inside lies on the same line, so everything that can be seen is drawn exactly as before; a
/// point inside the band, which is every point of any ordinary data, reaches the geometry unchanged.
/// </para>
/// <para>
/// Pure and deterministic: the same points and scale always give the same doubles. Not a renderer
/// abstraction, and not the start of one; the geometry is rebuilt whenever it is asked for, with no cache.
/// </para>
/// </remarks>
internal static class SeriesPlacement
{
    /// <summary>
    /// How far beyond the plot, in DIP, a segment is drawn before it is cut — the render guard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured, not guessed: WPF drew a segment reaching about 1.2 × 10⁵ DIP from the plot
    /// and dropped one reaching about 1.9 × 10⁵ DIP, at three device pixels per DIP — a limit near 2¹⁹ device
    /// pixels. 16 384 DIP beyond the plot keeps a plot on a 4K display, at four device pixels per DIP, below a
    /// sixth of that.
    /// </para>
    /// <para>
    /// Large enough never to be seen. With <see cref="FastiraChartControl.ClipToPlotArea"/> true the plot clip is
    /// far inside it; with it false the band still reaches further past the plot than any display is wide, so a
    /// line is not seen to end at it.
    /// </para>
    /// </remarks>
    internal const double RenderGuardMargin = 16384;

    /// <summary>
    /// The scale for a plot and two ranges, if every series can be placed by it.
    /// </summary>
    /// <returns>
    /// False when the plot has no interior, or either range has no scale — a degenerate, inverted or
    /// overflowing range. No series draws anything then, for the reason no grid line does.
    /// </returns>
    public static bool TryCreateScale(
        Rect plot,
        double horizontalMinimum,
        double horizontalMaximum,
        double verticalMinimum,
        double verticalMaximum,
        out SeriesScale scale)
    {
        scale = default;

        if (!MajorGridPlacement.HasInterior(plot)
            || !LinearAxisMapping.TryGetInverseRange(horizontalMinimum, horizontalMaximum, out var horizontalInverse)
            || !LinearAxisMapping.TryGetInverseRange(verticalMinimum, verticalMaximum, out var verticalInverse))
        {
            return false;
        }

        scale = new SeriesScale(plot, horizontalMinimum, horizontalMaximum, horizontalInverse, verticalMinimum, verticalMaximum, verticalInverse);

        return true;
    }

    /// <summary>
    /// The coordinate of one point, if it has one.
    /// </summary>
    /// <returns>False when a value, or the coordinate it maps to, is not finite.</returns>
    public static bool TryMap(in SeriesScale scale, ChartPoint point, out Point coordinate)
    {
        coordinate = default;

        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            return false;
        }

        var x = AxisTickPlacement.HorizontalCoordinate(
            scale.Plot,
            LinearAxisMapping.Position(point.X, scale.MinimumX, scale.MaximumX, scale.HorizontalInverseRange));
        var y = AxisTickPlacement.VerticalCoordinate(
            scale.Plot,
            LinearAxisMapping.Position(point.Y, scale.MinimumY, scale.MaximumY, scale.VerticalInverseRange));

        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            return false;
        }

        coordinate = new Point(x, y);

        return true;
    }

    /// <summary>
    /// Builds the geometry for one series: one figure per unbroken run of two or more placed points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A run's first point is held back until a second arrives, so a point isolated between two gaps opens no
    /// figure; with no marker to draw, it has nothing to show. A gap ends the open figure, and the next placed
    /// point starts a new one: the line never crosses a gap.
    /// </para>
    /// <para>
    /// Each segment passes through the render guard. One with both ends inside it — every segment of ordinary
    /// data — is written as it is. One that leaves the band ends the figure where it leaves, and one that comes
    /// back starts a new figure where it re-enters; one wholly outside writes nothing.
    /// </para>
    /// <para>
    /// Written point by point into one <see cref="StreamGeometry"/>, which keeps no object per point, and
    /// frozen once complete. The geometry is FastiraChart's own, so freezing it touches nothing of the
    /// caller's.
    /// </para>
    /// </remarks>
    public static SeriesGeometry Build(IReadOnlyList<ChartPoint> points, in SeriesScale scale)
    {
        StreamGeometry? geometry = null;
        StreamGeometryContext? context = null;
        int figures = 0, segments = 0, placed = 0, gaps = 0, trimmed = 0;
        var guard = Guard(scale.Plot);
        var open = false;
        var hasPrevious = false;
        var previous = default(Point);

        try
        {
            for (var i = 0; i < points.Count; i++)
            {
                if (!TryMap(scale, points[i], out var coordinate))
                {
                    gaps++;
                    open = false;
                    hasPrevious = false;

                    continue;
                }

                placed++;

                if (!hasPrevious)
                {
                    previous = coordinate;
                    hasPrevious = true;

                    continue;
                }

                var inside = TryGuard(previous, coordinate, guard, out var start, out var end, out var startCut, out var endCut);

                if (startCut || endCut || !inside)
                {
                    trimmed++;
                }

                if (inside)
                {
                    if (!open || startCut)
                    {
                        if (context is null)
                        {
                            geometry = new StreamGeometry();
                            context = geometry.Open();
                        }

                        context.BeginFigure(start, isFilled: false, isClosed: false);
                        figures++;
                    }

                    context!.LineTo(end, isStroked: true, isSmoothJoin: false);
                    segments++;

                    // A segment cut where it leaves the band ends its figure there; the next starts afresh.
                    open = !endCut;
                }
                else
                {
                    open = false;
                }

                previous = coordinate;
            }
        }
        finally
        {
            context?.Close();
        }

        if (geometry is null)
        {
            return new SeriesGeometry(null, 0, 0, placed, gaps, trimmed);
        }

        geometry.Freeze();

        return new SeriesGeometry(geometry, figures, segments, placed, gaps, trimmed);
    }

    /// <summary>The render guard around a plot: left, top, right and bottom.</summary>
    internal static (double Left, double Top, double Right, double Bottom) Guard(Rect plot)
        => (plot.Left - RenderGuardMargin, plot.Top - RenderGuardMargin, plot.Right + RenderGuardMargin, plot.Bottom + RenderGuardMargin);

    /// <summary>
    /// The part of a segment inside the render guard, if any (Liang–Barsky, in double precision).
    /// </summary>
    /// <param name="a">The segment's start.</param>
    /// <param name="b">The segment's end.</param>
    /// <param name="guard">The band to keep.</param>
    /// <param name="start">The kept part's start: <paramref name="a"/> itself when it is inside.</param>
    /// <param name="end">The kept part's end: <paramref name="b"/> itself when it is inside.</param>
    /// <param name="startCut">Whether the start was moved to the edge of the band.</param>
    /// <param name="endCut">Whether the end was moved to the edge of the band.</param>
    /// <returns>False when no part of the segment is inside.</returns>
    /// <remarks>
    /// An end that is inside is returned as the very point given, not recomputed as <c>a + 1 · (b − a)</c>,
    /// which can differ from <c>b</c> in the last bit: the guard must never move a point it has no reason to.
    /// The direction is held as half the difference, which cannot overflow even when the ends are at opposite
    /// extremes of the double range, and is exact otherwise — halving a double loses nothing.
    /// </remarks>
    internal static bool TryGuard(
        Point a,
        Point b,
        (double Left, double Top, double Right, double Bottom) guard,
        out Point start,
        out Point end,
        out bool startCut,
        out bool endCut)
    {
        start = a;
        end = b;
        startCut = false;
        endCut = false;

        var hx = (0.5 * b.X) - (0.5 * a.X);
        var hy = (0.5 * b.Y) - (0.5 * a.Y);
        double t0 = 0, t1 = 1;

        if (!Limit(-hx, a.X - guard.Left, ref t0, ref t1)
            || !Limit(hx, guard.Right - a.X, ref t0, ref t1)
            || !Limit(-hy, a.Y - guard.Top, ref t0, ref t1)
            || !Limit(hy, guard.Bottom - a.Y, ref t0, ref t1))
        {
            return false;
        }

        if (t0 > 0)
        {
            start = new Point(a.X + (t0 * hx) + (t0 * hx), a.Y + (t0 * hy) + (t0 * hy));
            startCut = true;
        }

        if (t1 < 1)
        {
            end = new Point(a.X + (t1 * hx) + (t1 * hx), a.Y + (t1 * hy) + (t1 * hy));
            endCut = true;
        }

        return true;
    }

    /// <summary>
    /// Narrows <c>[t0, t1]</c> by one edge, for a point <c>a + t · 2h</c> that must satisfy
    /// <c>t · 2h · sign ≤ distance</c>; false when nothing is left.
    /// </summary>
    /// <param name="h">Half the component of the direction, signed towards the edge.</param>
    /// <param name="distance">How far the start is inside the edge; negative when outside.</param>
    /// <param name="t0">The lowest <c>t</c> still kept, raised by an edge the segment enters through.</param>
    /// <param name="t1">The highest <c>t</c> still kept, lowered by an edge the segment leaves through.</param>
    private static bool Limit(double h, double distance, ref double t0, ref double t1)
    {
        if (h == 0)
        {
            return distance >= 0;
        }

        var t = (0.5 * distance) / h;

        if (h < 0)
        {
            if (t > t1)
            {
                return false;
            }

            if (t > t0)
            {
                t0 = t;
            }
        }
        else
        {
            if (t < t0)
            {
                return false;
            }

            if (t < t1)
            {
                t1 = t;
            }
        }

        return true;
    }
}
