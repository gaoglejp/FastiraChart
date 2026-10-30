using System.Windows;

namespace FastiraChart.Axes;

/// <summary>
/// One tick mark as a line segment, in the control's coordinate space.
/// </summary>
internal readonly record struct TickMark(Point Start, Point End);

/// <summary>
/// Turns ticks and a plot rectangle into tick mark geometry.
/// </summary>
/// <remarks>
/// <para>
/// The middle of the three stages — generation, placement, drawing — and pure like the first.
/// Keeping placement out of the render pass means the geometry can be computed once per
/// arrange rather than once per frame, and lets it be checked numerically without a window.
/// </para>
/// <para>
/// Every mark starts exactly on the plot edge and extends away from the plot, into the axis
/// band. Nothing is ever drawn into the plot interior: that space belongs to the grid.
/// </para>
/// </remarks>
internal static class AxisTickPlacement
{
    /// <summary>
    /// Appends the marks for a horizontal axis along the top or bottom edge.
    /// </summary>
    /// <param name="into">Collection to append to.</param>
    /// <param name="plot">The plot rectangle, in the same space the marks are produced in.</param>
    /// <param name="position">Which edge the axis occupies.</param>
    /// <param name="length">Effective tick length, already clamped to the axis band.</param>
    /// <param name="ticks">The ticks to place.</param>
    /// <remarks>
    /// The normalized position runs left to right, so it maps straight onto x.
    /// </remarks>
    public static void AppendHorizontal(
        IList<TickMark> into,
        Rect plot,
        HorizontalAxisPosition position,
        double length,
        IReadOnlyList<Tick> ticks)
    {
        if (ticks.Count == 0 || !IsUsable(length) || plot.Width <= 0)
        {
            return;
        }

        var edge = position == HorizontalAxisPosition.Bottom ? plot.Bottom : plot.Top;
        var tip = position == HorizontalAxisPosition.Bottom ? edge + length : edge - length;

        foreach (var tick in ticks)
        {
            var x = HorizontalCoordinate(plot, tick);

            into.Add(new TickMark(new Point(x, edge), new Point(x, tip)));
        }
    }

    /// <summary>
    /// Appends the marks for a vertical axis along the left or right edge.
    /// </summary>
    /// <param name="into">Collection to append to.</param>
    /// <param name="plot">The plot rectangle, in the same space the marks are produced in.</param>
    /// <param name="position">Which edge the axis occupies.</param>
    /// <param name="length">Effective tick length, already clamped to the axis band.</param>
    /// <param name="ticks">The ticks to place.</param>
    /// <remarks>
    /// The normalized position runs bottom to top while y grows downward, so it is subtracted
    /// from the bottom edge rather than added to the top.
    /// </remarks>
    public static void AppendVertical(
        IList<TickMark> into,
        Rect plot,
        VerticalAxisPosition position,
        double length,
        IReadOnlyList<Tick> ticks)
    {
        if (ticks.Count == 0 || !IsUsable(length) || plot.Height <= 0)
        {
            return;
        }

        var edge = position == VerticalAxisPosition.Left ? plot.Left : plot.Right;
        var tip = position == VerticalAxisPosition.Left ? edge - length : edge + length;

        foreach (var tick in ticks)
        {
            var y = VerticalCoordinate(plot, tick);

            into.Add(new TickMark(new Point(edge, y), new Point(tip, y)));
        }
    }

    /// <summary>
    /// The x coordinate of a tick on a horizontal axis.
    /// </summary>
    /// <remarks>
    /// Public, and used by the mark placement above rather than duplicated inside it, so that a
    /// label and its tick cannot drift apart — nor a grid line and its tick. Two
    /// expressions that happen to agree today are two expressions that can stop agreeing; one
    /// expression cannot. The normalized position runs left to right, so it maps straight onto x.
    /// </remarks>
    public static double HorizontalCoordinate(Rect plot, Tick tick)
        => HorizontalCoordinate(plot, tick.NormalizedPosition);

    /// <summary>
    /// The x coordinate of a normalized position on a horizontal axis.
    /// </summary>
    /// <remarks>
    /// The expression itself, which the <see cref="Tick"/> overload above delegates to. Exposed so that
    /// a value that is not a tick — an origin or a reference line — is placed by the same
    /// expression as a tick at the same normalized position, and so lands on the same double.
    /// </remarks>
    public static double HorizontalCoordinate(Rect plot, double normalizedPosition)
        => plot.Left + (normalizedPosition * plot.Width);

    /// <summary>
    /// The y coordinate of a tick on a vertical axis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The single source for a vertical tick's position, for the reason above. The normalized
    /// position runs bottom to top while y grows downward, so the distance is measured down from
    /// the top edge by the complement, <c>1 − n</c>.
    /// </para>
    /// <para>
    /// Written this way so both endpoints are exact, which the obvious form is not. It used to be
    /// <c>Bottom − n·Height</c>; at <c>n = 1</c> that is <c>(Y + Height) − Height</c>, which rounds
    /// back to something other than <c>Y</c> for most fractional plots — 10.039999999999992 for a
    /// plot whose top is 10.04 — putting the top tick, its label and its grid line a few
    /// ulps above the plot. An exact comparison of grid lines against the plot edge shows it, where a
    /// tolerance of 0.0001 would not.
    /// </para>
    /// <para>
    /// This form gives <c>Top</c> exactly at <c>n = 1</c> and <c>Top + Height</c> — the very
    /// expression <see cref="Rect.Bottom"/> evaluates — at <c>n = 0</c>. In between, rounding is
    /// monotonic, so <c>(1 − n)·Height</c> never exceeds <c>Height</c> and every coordinate lies
    /// within <c>[Top, Bottom]</c> without a clamp. The horizontal form above already had this
    /// property: <c>Left + 1·Width</c> is <c>Right</c>'s own expression.
    /// </para>
    /// </remarks>
    public static double VerticalCoordinate(Rect plot, Tick tick)
        => VerticalCoordinate(plot, tick.NormalizedPosition);

    /// <summary>
    /// The y coordinate of a normalized position on a vertical axis.
    /// </summary>
    /// <remarks>
    /// The form described above, which the <see cref="Tick"/> overload delegates to: exact at both
    /// edges and never outside them. Origin and reference lines are placed through it, so a horizontal
    /// line at a value lands exactly where that value's tick and grid line do.
    /// </remarks>
    public static double VerticalCoordinate(Rect plot, double normalizedPosition)
        => plot.Top + ((1 - normalizedPosition) * plot.Height);

    /// <summary>
    /// Where an x coordinate lies across a horizontal axis, as fractions of the plot's width: the inverse of
    /// <see cref="HorizontalCoordinate(Rect, double)"/>.
    /// </summary>
    /// <param name="plot">The rectangle the position runs across; it must have a width.</param>
    /// <param name="x">The coordinate, in the same space as <paramref name="plot"/>.</param>
    /// <returns>
    /// <c>FromMinimum</c>, the distance from the left edge — the normalized position itself — and <c>FromMaximum</c>, the
    /// distance from the right edge. Each is 0 exactly on its own edge; outside the plot one of them is negative.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Both, each measured from its own edge, rather than one and its complement: <c>1 − n</c> rounds where <c>n</c> is
    /// small, and a value read from the far end through it inherits that rounding. Each edge is then exact as written —
    /// the right edge is <see cref="Rect.Right"/>, the forward expression at 1 — and <see cref="LinearAxisMapping.Value"/>
    /// reads the value from whichever end is nearer.
    /// </para>
    /// </remarks>
    public static (double FromMinimum, double FromMaximum) HorizontalFractions(Rect plot, double x)
        => ((x - plot.Left) / plot.Width, (plot.Right - x) / plot.Width);

    /// <summary>
    /// Where a y coordinate lies up a vertical axis, as fractions of the plot's height: the inverse of
    /// <see cref="VerticalCoordinate(Rect, double)"/>.
    /// </summary>
    /// <param name="plot">The rectangle the position runs up; it must have a height.</param>
    /// <param name="y">The coordinate, in the same space as <paramref name="plot"/>; it grows downward.</param>
    /// <returns>
    /// <c>FromMinimum</c>, the distance up from the bottom edge — the normalized position itself — and <c>FromMaximum</c>,
    /// the distance down from the top edge, which is the forward's own <c>1 − n</c>. Each is 0 exactly on its own edge.
    /// </returns>
    /// <remarks>
    /// The y axis grows downward while values grow upward, so the minimum's edge is <see cref="Rect.Bottom"/> — the forward
    /// expression at 0 — and the maximum's is <see cref="Rect.Top"/>. Measured from both, for the reason given at
    /// <see cref="HorizontalFractions"/>.
    /// </remarks>
    public static (double FromMinimum, double FromMaximum) VerticalFractions(Rect plot, double y)
        => ((plot.Bottom - y) / plot.Height, (y - plot.Top) / plot.Height);

    /// <summary>
    /// The tick length actually usable on an edge.
    /// </summary>
    /// <param name="requested">The requested length.</param>
    /// <param name="availableAxisSpace">The effective axis space granted on that edge.</param>
    /// <returns>A finite, non-negative length that never leaves the axis band.</returns>
    /// <remarks>
    /// A tick must not escape the band reserved for it, so the length is capped by the space
    /// that was actually granted — which, as the layout guarantees, can be less than was asked for
    /// and can be zero. Zero is a normal outcome, not a fault: the axis sits on the plot edge
    /// and draws no marks.
    /// </remarks>
    public static double EffectiveTickLength(double requested, double availableAxisSpace)
    {
        if (!IsUsable(requested) || !IsUsable(availableAxisSpace))
        {
            return 0;
        }

        return Math.Min(requested, availableAxisSpace);
    }

    private static bool IsUsable(double value) => double.IsFinite(value) && value > 0;
}
