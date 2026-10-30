using System.Windows;
using FastiraChart.Axes;
using FastiraChart.Gridlines;
using FastiraChart.Lens;
using FastiraChart.Series;

namespace FastiraChart.Interaction;

/// <summary>Where a pointer is over the chart.</summary>
internal enum PointerRegion
{
    /// <summary>Outside the plot: an axis band, the padding, the border, or a plot with no interior.</summary>
    Outside,

    /// <summary>Inside the plot, edges included, and not inside the lens.</summary>
    Plot,

    /// <summary>Inside the drawn lens, edges included. The lens is always inside the plot.</summary>
    Lens,
}

/// <summary>
/// One pointer position resolved against the chart: where it is, and the values it stands for.
/// </summary>
/// <param name="Screen">The position in the control's own space, in DIP — what <c>MouseEventArgs.GetPosition(control)</c> gives.</param>
/// <param name="Region">Which part of the chart the position is in.</param>
/// <param name="HasData">Whether the main mapping gives values here: inside the plot, and both ranges have a scale.</param>
/// <param name="Normalized">The main normalized position, X left to right and Y bottom to top, 0..1; when <paramref name="HasData"/>.</param>
/// <param name="Data">The main X and Y values; when <paramref name="HasData"/>.</param>
/// <param name="HasLensData">Whether the lens mapping gives values here: inside an active lens, and the vertical range has a scale.</param>
/// <param name="LensNormalized">The normalized position across the lens and up the plot; when <paramref name="HasLensData"/>.</param>
/// <param name="LensData">The lens X value (in the effective lens source) and the Y value; when <paramref name="HasLensData"/>.</param>
/// <remarks>
/// <para>
/// <b>Screen → plot → normalized → data</b>, for the main chart and the lens alike: <see cref="PointerMapping.TryMap"/> on the
/// main <see cref="SeriesScale"/>, and on the lens's own (<see cref="LensMapping.TryCreateScale"/>). Values that are not
/// available read as 0; the flags say which are.
/// </para>
/// <para>
/// A small immutable value: resolving a pointer allocates nothing.
/// </para>
/// </remarks>
internal readonly record struct ChartPointerPosition(
    Point Screen,
    PointerRegion Region,
    bool HasData,
    Point Normalized,
    ChartPoint Data,
    bool HasLensData,
    Point LensNormalized,
    ChartPoint LensData)
{
    /// <summary>Whether the position is inside the plot, edges and the lens included.</summary>
    public bool IsInsidePlot => Region != PointerRegion.Outside;

    /// <summary>Whether the position is inside the drawn lens, edges included.</summary>
    public bool IsInsideLens => Region == PointerRegion.Lens;
}

/// <summary>
/// Turns a pointer coordinate back into chart values: the inverse of the placement every layer draws by.
/// </summary>
/// <remarks>
/// <para>
/// <b>No coordinate system of its own.</b> A coordinate goes back through
/// <see cref="AxisTickPlacement.HorizontalFractions"/> or <see cref="AxisTickPlacement.VerticalFractions"/> and then
/// <see cref="LinearAxisMapping.Value"/> — the inverses, written beside them, of the very expressions
/// <see cref="SeriesPlacement.TryMap"/> places a point by. The lens is not a special case: it is a
/// <see cref="SeriesScale"/> whose rectangle is the lens and whose horizontal range is the effective lens source, the scale
/// the lens content is drawn by, so the same inverse reads it.
/// </para>
/// <para>
/// <b>Hit regions are the rectangles, nothing else.</b> The plot is the plot rectangle every layer is placed against; the
/// lens is its effective rectangle. Neither depends on what is drawn, its z-order or its clip: a series drawn past the plot
/// with <see cref="FastiraChartControl.ClipToPlotArea"/> off does not make that area part of the plot.
/// </para>
/// <para>
/// <b>Outside, no value.</b> A coordinate outside the rectangle has no value here — the answer is false, not the nearest
/// edge — as <see cref="LinearAxisMapping.TryNormalize"/> gives no position to a value outside its range. Edges are inside,
/// and map to the range's ends exactly.
/// </para>
/// <para>
/// Pure, deterministic and allocation-free; nothing is rounded to a device pixel.
/// </para>
/// </remarks>
internal static class PointerMapping
{
    /// <summary>Whether a finite point lies in a rectangle, edges included.</summary>
    /// <remarks>
    /// The edges are <see cref="Rect.Right"/> and <see cref="Rect.Bottom"/> as WPF computes them — the coordinates the
    /// placement puts the range's ends at — so a pointer on the last grid line is inside.
    /// </remarks>
    public static bool Contains(Rect bounds, Point point)
        => double.IsFinite(point.X) && double.IsFinite(point.Y)
        && point.X >= bounds.Left && point.X <= bounds.Right
        && point.Y >= bounds.Top && point.Y <= bounds.Bottom;

    /// <summary>
    /// The values a coordinate stands for under a scale, if it lies inside the scale's rectangle: the inverse of
    /// <see cref="SeriesPlacement.TryMap"/>.
    /// </summary>
    /// <param name="scale">The scale; from <see cref="SeriesPlacement.TryCreateScale"/> or <see cref="LensMapping.TryCreateScale"/>.</param>
    /// <param name="coordinate">The coordinate, in the scale's space.</param>
    /// <param name="normalized">The normalized position, each axis 0..1.</param>
    /// <param name="value">The X and Y values, each within its range.</param>
    /// <returns>False outside the rectangle, for a non-finite coordinate, and for a scale with no rectangle.</returns>
    /// <remarks>
    /// Inside the rectangle each fraction is held to 0..1, and each value to its range — the forward
    /// <see cref="LinearAxisMapping.Normalize"/> holds its position the same way — so a coordinate a rounding hair inside an
    /// edge never reads a value outside the range. Nothing is held for a coordinate outside: it has no value.
    /// </remarks>
    public static bool TryMap(in SeriesScale scale, Point coordinate, out Point normalized, out ChartPoint value)
    {
        normalized = default;
        value = default;

        if (!MajorGridPlacement.HasInterior(scale.Plot) || !Contains(scale.Plot, coordinate))
        {
            return false;
        }

        var (left, right) = AxisTickPlacement.HorizontalFractions(scale.Plot, coordinate.X);
        var (bottom, top) = AxisTickPlacement.VerticalFractions(scale.Plot, coordinate.Y);

        left = Math.Clamp(left, 0, 1);
        right = Math.Clamp(right, 0, 1);
        bottom = Math.Clamp(bottom, 0, 1);
        top = Math.Clamp(top, 0, 1);

        var x = Math.Clamp(LinearAxisMapping.Value(left, right, scale.MinimumX, scale.MaximumX), scale.MinimumX, scale.MaximumX);
        var y = Math.Clamp(LinearAxisMapping.Value(bottom, top, scale.MinimumY, scale.MaximumY), scale.MinimumY, scale.MaximumY);

        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            return false;
        }

        normalized = new Point(left, bottom);
        value = new ChartPoint(x, y);

        return true;
    }

    /// <summary>
    /// Resolves a pointer coordinate against a plot, the main ranges and the lens state.
    /// </summary>
    /// <param name="screen">The pointer, in the control's space.</param>
    /// <param name="plot">The rectangle every layer is placed against, in the control's space.</param>
    /// <param name="horizontalMinimum">The main horizontal minimum.</param>
    /// <param name="horizontalMaximum">The main horizontal maximum.</param>
    /// <param name="verticalMinimum">The main vertical minimum.</param>
    /// <param name="verticalMaximum">The main vertical maximum.</param>
    /// <param name="lens">The lens state as requested; placed here by <see cref="LensMapping.TryPlace"/>, as the lens layer places it.</param>
    /// <remarks>
    /// <para>
    /// The plot region is the plot rectangle when it has an interior. Main values need both ranges to have a scale, as a
    /// series needs both to be drawn; a degenerate or inverted range gives the region and no values.
    /// </para>
    /// <para>
    /// The lens region is the effective lens rectangle when the lens is active — shown, with a source inside the main range,
    /// and room in the plot. An inactive lens has no rectangle, so a pointer where it would stand is in the plot, with main
    /// values only. Inside the lens X is read from the effective source, never the requested one, and Y from the main
    /// vertical range over the plot's own top and height: the same expression on the same doubles as the main Y.
    /// </para>
    /// </remarks>
    public static ChartPointerPosition Resolve(
        Point screen,
        Rect plot,
        double horizontalMinimum,
        double horizontalMaximum,
        double verticalMinimum,
        double verticalMaximum,
        in LensState lens)
    {
        if (!MajorGridPlacement.HasInterior(plot) || !Contains(plot, screen))
        {
            return new ChartPointerPosition(screen, PointerRegion.Outside, false, default, default, false, default, default);
        }

        var normalized = default(Point);
        var data = default(ChartPoint);
        var hasData = SeriesPlacement.TryCreateScale(plot, horizontalMinimum, horizontalMaximum, verticalMinimum, verticalMaximum, out var scale)
            && TryMap(scale, screen, out normalized, out data);

        if (!LensMapping.TryPlace(lens, plot, horizontalMinimum, horizontalMaximum, out var placement)
            || !Contains(placement.Bounds, screen))
        {
            return new ChartPointerPosition(screen, PointerRegion.Plot, hasData, normalized, data, false, default, default);
        }

        var lensNormalized = default(Point);
        var lensData = default(ChartPoint);
        var hasLensData = LensMapping.TryCreateScale(placement, verticalMinimum, verticalMaximum, out var lensScale)
            && TryMap(lensScale, screen, out lensNormalized, out lensData);

        return new ChartPointerPosition(screen, PointerRegion.Lens, hasData, normalized, data, hasLensData, lensNormalized, lensData);
    }
}
