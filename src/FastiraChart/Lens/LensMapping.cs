using System.Windows;
using FastiraChart.Axes;
using FastiraChart.Gridlines;
using FastiraChart.Series;

namespace FastiraChart.Lens;

/// <summary>
/// What the caller asked of the lens: whether it is shown, which X values it magnifies, and where and how wide it is
/// drawn.
/// </summary>
/// <param name="IsVisible">Whether the lens is shown at all.</param>
/// <param name="SourceMinimum">The smallest X value the lens is asked to show.</param>
/// <param name="SourceMaximum">The largest X value the lens is asked to show.</param>
/// <param name="HorizontalPosition">
/// Where the lens stands along the plot, from 0 to 1: 0 puts its left edge on the plot's left edge, 1 its right edge on
/// the plot's right edge, 0.5 centres it. A fraction of the travel the lens has, not of the plot, so every value in 0..1
/// keeps the whole lens inside the plot.
/// </param>
/// <param name="Width">The display width asked for, in DIP.</param>
/// <remarks>
/// <para>
/// The lens state, as requested — never altered by what the plot allows. What is actually drawn is derived
/// from it by <see cref="LensMapping.TryPlace"/>: the source intersected with the main horizontal range, the width capped
/// by the plot's.
/// </para>
/// <para>
/// Internal: the public lens API is the control's lens properties.
/// </para>
/// </remarks>
internal readonly record struct LensState(
    bool IsVisible,
    double SourceMinimum,
    double SourceMaximum,
    double HorizontalPosition,
    double Width);

/// <summary>
/// Where the lens is drawn and which X values it shows there: the horizontal half of the lens mapping.
/// </summary>
/// <param name="Bounds">
/// The lens's display rectangle, in the control's space: the plot's full height — its very <see cref="Rect.Top"/> and
/// <see cref="Rect.Height"/> — and a horizontal part of it.
/// </param>
/// <param name="SourceMinimum">The smallest X value shown: the requested source intersected with the main range.</param>
/// <param name="SourceMaximum">The largest X value shown.</param>
/// <param name="SourceInverseRange">
/// <c>1 / (SourceMaximum − SourceMinimum)</c>, computed by <see cref="LinearAxisMapping.TryGetInverseRange"/>, as every
/// range's reciprocal is.
/// </param>
internal readonly record struct LensPlacement(
    Rect Bounds,
    double SourceMinimum,
    double SourceMaximum,
    double SourceInverseRange)
{
    /// <summary>The lens X coordinate of an X value, inside the source or outside it.</summary>
    /// <remarks>
    /// The main chart's own expressions with the lens's rectangle and source in place of the plot and the main range:
    /// <see cref="LinearAxisMapping.Position"/>, then <see cref="AxisTickPlacement.HorizontalCoordinate(Rect, double)"/>.
    /// A value outside the source lands outside the lens, and the lens clip decides what is seen, as for a series point
    /// off the plot.
    /// </remarks>
    public double X(double value)
        => AxisTickPlacement.HorizontalCoordinate(
            Bounds,
            LinearAxisMapping.Position(value, SourceMinimum, SourceMaximum, SourceInverseRange));

    /// <summary>Whether an X value lies in the lens source, endpoints included.</summary>
    public bool Contains(double value) => value >= SourceMinimum && value <= SourceMaximum;

    /// <summary>
    /// How many times wider one X unit is drawn in the lens than in the main plot.
    /// </summary>
    /// <param name="plot">The main plot.</param>
    /// <param name="mainMinimum">The main horizontal minimum.</param>
    /// <param name="mainMaximum">The main horizontal maximum.</param>
    /// <returns>The ratio of the two scales, in DIP per X unit; above 1 is magnified.</returns>
    public double Magnification(Rect plot, double mainMinimum, double mainMaximum)
        => (Bounds.Width / (SourceMaximum - SourceMinimum)) / (plot.Width / (mainMaximum - mainMinimum));
}

/// <summary>
/// Turns the lens state, the plot and the main ranges into the lens's rectangle and mapping.
/// </summary>
/// <remarks>
/// <para>
/// <b>Vertical: the whole chart. Horizontal: a part of it.</b> The lens is the plot's full height, and maps Y exactly as
/// the main chart does; it takes a horizontal part of the plot and draws a narrower X range across it, magnified.
/// </para>
/// <para>
/// <b>No new scale.</b> The lens is placed by the main chart's own mapping: <see cref="TryCreateScale"/> hands back a
/// <see cref="SeriesScale"/> — the type every series point is placed by — whose plot is the lens rectangle and whose
/// horizontal range is the lens source. Its vertical range is the main vertical range, and the lens rectangle has the
/// plot's very top and height, so a Y value reaches the same double in the lens as in the main chart: the same
/// expression on the same inputs, not a second calculation that agrees.
/// </para>
/// <para>
/// <b>Intersection, not clamping.</b> A source reaching outside the main horizontal range is cut to the part inside it;
/// a source with no part inside it leaves the lens inactive. Nothing is moved onto an edge, as no reference line or tick
/// ever is: the lens shows the values the chart shows, or nothing.
/// </para>
/// <para>
/// Pure and deterministic: the same inputs give the same doubles, and nothing is rounded to a device pixel.
/// </para>
/// </remarks>
internal static class LensMapping
{
    /// <summary>
    /// The part of the requested source the main horizontal range shows, if any.
    /// </summary>
    /// <returns>
    /// False when the requested source is not an interval (inverted, degenerate, non-finite), the main range has no
    /// scale, or the two do not overlap in more than a point.
    /// </returns>
    public static bool TryEffectiveSource(
        double requestedMinimum,
        double requestedMaximum,
        double mainMinimum,
        double mainMaximum,
        out double minimum,
        out double maximum)
    {
        minimum = 0;
        maximum = 0;

        // The request needs only to be an interval: finite, minimum below maximum. One wider than a double can span is
        // still an interval, and its part inside the main range is what is drawn. The main range needs a scale.
        if (!double.IsFinite(requestedMinimum) || !double.IsFinite(requestedMaximum) || !(requestedMaximum > requestedMinimum)
            || !LinearAxisMapping.TryGetInverseRange(mainMinimum, mainMaximum, out _))
        {
            return false;
        }

        var low = Math.Max(requestedMinimum, mainMinimum);
        var high = Math.Min(requestedMaximum, mainMaximum);

        if (!(high > low))
        {
            return false;
        }

        minimum = low;
        maximum = high;

        return true;
    }

    /// <summary>
    /// The lens rectangle for a plot, a requested width and a position, if the plot has room for one.
    /// </summary>
    /// <param name="plot">The main plot, in the control's space.</param>
    /// <param name="requestedWidth">The width asked for; finite and non-negative.</param>
    /// <param name="position">Where along its travel the lens stands, 0 to 1.</param>
    /// <param name="bounds">The rectangle: the plot's top and height, a width of at most the plot's, inside it.</param>
    /// <returns>False for a plot with no interior, or a width or position that gives the lens no width.</returns>
    /// <remarks>
    /// <para>
    /// The effective width is <c>min(requested, plot width)</c>; the requested width is not touched. The left edge is
    /// <c>plot.Left + position · (plot.Width − width)</c>, so 0 is flush left, 1 flush right, and every position between
    /// keeps the lens inside the plot without a clamp on the position.
    /// </para>
    /// <para>
    /// The right edge is held to the plot's: should <c>left + width</c> round a hair past it, the width is taken down by
    /// the ulp that did, so the rectangle is never outside the plot.
    /// </para>
    /// </remarks>
    public static bool TryEffectiveBounds(Rect plot, double requestedWidth, double position, out Rect bounds)
    {
        bounds = default;

        if (!MajorGridPlacement.HasInterior(plot)
            || !double.IsFinite(requestedWidth) || !(requestedWidth > 0)
            || !double.IsFinite(position))
        {
            return false;
        }

        var width = Math.Min(requestedWidth, plot.Width);
        var travel = plot.Width - width;
        var left = plot.Left + (Math.Clamp(position, 0, 1) * travel);

        while (width > 0 && left + width > plot.Right)
        {
            width = Math.BitDecrement(width);
        }

        if (!(width > 0) || !double.IsFinite(left))
        {
            return false;
        }

        bounds = new Rect(left, plot.Top, width, plot.Height);

        return true;
    }

    /// <summary>
    /// The lens's rectangle and source for a state, a plot and the main horizontal range, if the lens is drawn.
    /// </summary>
    /// <returns>
    /// False when the lens is hidden, its source has no part inside the main range, or the plot gives it no room: then it
    /// has no geometry and draws nothing.
    /// </returns>
    public static bool TryPlace(in LensState state, Rect plot, double mainMinimum, double mainMaximum, out LensPlacement placement)
    {
        placement = default;

        if (!state.IsVisible
            || !TryEffectiveSource(state.SourceMinimum, state.SourceMaximum, mainMinimum, mainMaximum, out var minimum, out var maximum)
            || !LinearAxisMapping.TryGetInverseRange(minimum, maximum, out var inverse)
            || !TryEffectiveBounds(plot, state.Width, state.HorizontalPosition, out var bounds))
        {
            return false;
        }

        placement = new LensPlacement(bounds, minimum, maximum, inverse);

        return true;
    }

    /// <summary>
    /// The scale a point is placed by inside the lens: the lens rectangle and source across, the main range down.
    /// </summary>
    /// <remarks>
    /// <see cref="SeriesPlacement.TryCreateScale"/> itself, handed the lens's rectangle and source: a lens point goes
    /// through <see cref="SeriesPlacement.TryMap"/> exactly as a main point does. The vertical half — the plot's top and
    /// height, the main vertical range and its reciprocal — is the main chart's own.
    /// </remarks>
    public static bool TryCreateScale(in LensPlacement placement, double verticalMinimum, double verticalMaximum, out SeriesScale scale)
        => SeriesPlacement.TryCreateScale(
            placement.Bounds,
            placement.SourceMinimum,
            placement.SourceMaximum,
            verticalMinimum,
            verticalMaximum,
            out scale);
}
