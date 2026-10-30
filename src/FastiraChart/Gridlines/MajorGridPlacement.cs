using System.Windows;
using FastiraChart.Axes;

namespace FastiraChart.Gridlines;

/// <summary>
/// One grid line as a line segment, in the control's coordinate space.
/// </summary>
/// <remarks>
/// Two points and nothing else: no brush, no thickness, no visual. Styling belongs to the drawing
/// stage, and a geometry model that carried it would make two lines in the same place compare
/// unequal for a reason that has nothing to do with where they are.
/// </remarks>
internal readonly record struct GridLine(Point Start, Point End);

/// <summary>
/// Turns already-generated major ticks and a plot rectangle into major grid line geometry.
/// </summary>
/// <remarks>
/// <para>
/// <b>The grid does not decide where it goes.</b> It is handed the very tick sequence the axis
/// drew, and maps each tick through the same coordinate helpers the tick marks and the labels use —
/// <see cref="AxisTickPlacement.HorizontalCoordinate(Rect, Tick)"/> and
/// <see cref="AxisTickPlacement.VerticalCoordinate(Rect, Tick)"/>. There is no step, range or count computed
/// here, and so no second answer that could disagree with the axis by a single DIP. A grid line and
/// its tick are not two calculations that agree; they are one calculation read twice.
/// </para>
/// <para>
/// <b>Naming is by the direction the line runs</b>, not by the axis it comes from, and the two are
/// crossed: a <em>horizontal</em> grid line comes from a tick on the <em>vertical</em> axis, and a
/// <em>vertical</em> grid line from a tick on the <em>horizontal</em> axis. Every method here names
/// the line it produces and takes the ticks it needs by their axis, so the crossing is visible at
/// each call site rather than remembered.
/// </para>
/// <para>
/// Pure and deterministic, like the tick placement stage it sits beside: the same ticks and the same
/// rectangle always yield the same doubles, and nothing here touches WPF rendering.
/// </para>
/// <para>
/// Every line spans the plot exactly, edge to edge, and never leaves it. The geometry is defined by
/// <see cref="FastiraChartControl.PlotBounds"/> alone, so neither <see cref="FastiraChartControl.ClipToPlotArea"/>
/// nor an axis band can lengthen it.
/// </para>
/// </remarks>
internal static class MajorGridPlacement
{
    /// <summary>
    /// Appends one horizontal grid line per vertical-axis major tick.
    /// </summary>
    /// <param name="into">Collection to append to.</param>
    /// <param name="plot">The plot rectangle, in the same space the ticks are placed in.</param>
    /// <param name="verticalAxisTicks">The vertical axis's ticks, exactly as the axis placed them.</param>
    /// <remarks>
    /// Each line runs from the plot's left edge to its right edge at the tick's y. A plot with no
    /// width or no height has no interior to cross, and yields no lines rather than lines of zero
    /// length or lines stacked on one coordinate.
    /// </remarks>
    public static void AppendHorizontal(IList<GridLine> into, Rect plot, IReadOnlyList<Tick> verticalAxisTicks)
    {
        if (verticalAxisTicks.Count == 0 || !HasInterior(plot))
        {
            return;
        }

        foreach (var tick in verticalAxisTicks)
        {
            var y = AxisTickPlacement.VerticalCoordinate(plot, tick);

            into.Add(new GridLine(new Point(plot.Left, y), new Point(plot.Right, y)));
        }
    }

    /// <summary>
    /// Appends one vertical grid line per horizontal-axis major tick.
    /// </summary>
    /// <param name="into">Collection to append to.</param>
    /// <param name="plot">The plot rectangle, in the same space the ticks are placed in.</param>
    /// <param name="horizontalAxisTicks">The horizontal axis's ticks, exactly as the axis placed them.</param>
    /// <remarks>
    /// Each line runs from the plot's top edge to its bottom edge at the tick's x.
    /// </remarks>
    public static void AppendVertical(IList<GridLine> into, Rect plot, IReadOnlyList<Tick> horizontalAxisTicks)
    {
        if (horizontalAxisTicks.Count == 0 || !HasInterior(plot))
        {
            return;
        }

        foreach (var tick in horizontalAxisTicks)
        {
            var x = AxisTickPlacement.HorizontalCoordinate(plot, tick);

            into.Add(new GridLine(new Point(x, plot.Top), new Point(x, plot.Bottom)));
        }
    }

    /// <summary>Whether the plot has an interior for a grid line to cross.</summary>
    /// <remarks>
    /// Both extents are required for either direction. A plot of zero height still has a width, but
    /// a vertical line across it has no length, and a horizontal line on it would sit on the axis
    /// line with nothing above or below it to be a grid for. The finiteness checks are the same
    /// defence the rest of the control keeps: <see cref="FastiraChartControl.PlotBounds"/> is
    /// guaranteed finite, and this does not take the guarantee on trust.
    /// <para>
    /// Internal rather than private: origin and reference lines use the same test, so a plot
    /// that has no grid has no origin or reference line either, for the same reason.
    /// </para>
    /// </remarks>
    internal static bool HasInterior(Rect plot)
        => !plot.IsEmpty
        && double.IsFinite(plot.X) && double.IsFinite(plot.Y)
        && double.IsFinite(plot.Width) && double.IsFinite(plot.Height)
        && plot.Width > 0 && plot.Height > 0;
}
