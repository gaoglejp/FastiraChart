using System.Windows;
using FastiraChart.Axes;
using FastiraChart.Gridlines;

namespace FastiraChart.ReferenceLines;

/// <summary>
/// One origin or reference line as a line segment, in the control's coordinate space.
/// </summary>
/// <remarks>
/// Two points and nothing else, for the reason <see cref="GridLine"/> gives: styling belongs to the
/// drawing stage, and two lines in the same place must compare equal.
/// </remarks>
internal readonly record struct PlotLine(Point Start, Point End);

/// <summary>
/// Turns a value on an axis and a plot rectangle into the line that marks it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No scale of its own.</b> A value becomes a normalized position through
/// <see cref="LinearAxisMapping.TryNormalize"/> — the very mapping the tick generator uses — and that
/// position becomes a coordinate through <see cref="AxisTickPlacement.HorizontalCoordinate(Rect, double)"/>
/// or <see cref="AxisTickPlacement.VerticalCoordinate(Rect, double)"/> — the very expressions the tick
/// marks, the labels and the grid use. So a reference line, an origin line and a grid line at one value
/// reach one coordinate, as one calculation rather than three that happen to agree.
/// </para>
/// <para>
/// <b>Named by the direction the line runs</b>, like <see cref="MajorGridPlacement"/>, and crossed the
/// same way: a <em>vertical</em> line marks a value on the <em>horizontal</em> axis (an x value), a
/// <em>horizontal</em> line a value on the <em>vertical</em> axis (a y value).
/// </para>
/// <para>
/// <b>Range, not ticks, decides.</b> Whether a value has a line depends on whether the range includes it,
/// endpoints included, and on nothing else — in particular not on whether a tick happens to be at that
/// value. A value outside the range has no line; it is not clamped to an edge.
/// </para>
/// <para>
/// Every line spans the plot exactly, edge to edge, and never leaves it. The geometry is defined by the
/// plot rectangle alone, so neither <see cref="FastiraChartControl.ClipToPlotArea"/> nor an axis band can
/// lengthen it. Pure and deterministic: the same inputs always yield the same doubles.
/// </para>
/// </remarks>
internal static class ReferenceLinePlacement
{
    /// <summary>
    /// The vertical line at a value on the horizontal axis, if the value has a position.
    /// </summary>
    /// <param name="plot">The plot rectangle, in the space the ticks are placed in.</param>
    /// <param name="minimum">The horizontal axis minimum.</param>
    /// <param name="maximum">The horizontal axis maximum.</param>
    /// <param name="value">The x value to mark.</param>
    /// <param name="line">The line from the plot's top edge to its bottom edge at the value's x.</param>
    /// <returns>
    /// False when the plot has no interior, the range has no scale, or the value is outside it.
    /// </returns>
    public static bool TryVertical(Rect plot, double minimum, double maximum, double value, out PlotLine line)
    {
        line = default;

        if (!MajorGridPlacement.HasInterior(plot)
            || !LinearAxisMapping.TryNormalize(value, minimum, maximum, out var normalized))
        {
            return false;
        }

        var x = AxisTickPlacement.HorizontalCoordinate(plot, normalized);

        line = new PlotLine(new Point(x, plot.Top), new Point(x, plot.Bottom));

        return true;
    }

    /// <summary>
    /// The horizontal line at a value on the vertical axis, if the value has a position.
    /// </summary>
    /// <param name="plot">The plot rectangle, in the space the ticks are placed in.</param>
    /// <param name="minimum">The vertical axis minimum.</param>
    /// <param name="maximum">The vertical axis maximum.</param>
    /// <param name="value">The y value to mark.</param>
    /// <param name="line">The line from the plot's left edge to its right edge at the value's y.</param>
    /// <returns>
    /// False when the plot has no interior, the range has no scale, or the value is outside it.
    /// </returns>
    public static bool TryHorizontal(Rect plot, double minimum, double maximum, double value, out PlotLine line)
    {
        line = default;

        if (!MajorGridPlacement.HasInterior(plot)
            || !LinearAxisMapping.TryNormalize(value, minimum, maximum, out var normalized))
        {
            return false;
        }

        var y = AxisTickPlacement.VerticalCoordinate(plot, normalized);

        line = new PlotLine(new Point(plot.Left, y), new Point(plot.Right, y));

        return true;
    }
}
