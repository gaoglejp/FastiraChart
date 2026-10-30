namespace FastiraChart.ReferenceLines;

/// <summary>
/// The value dimension a reference line marks: an X value or a Y value.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named by the value, not by the direction the line runs</b> — and the two are crossed:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="X"/> — an X-value reference line, drawn as a <em>vertical</em> screen line from the plot's top edge to its
/// bottom edge, at that X value on the X axis (<see cref="FastiraChartControl.MinimumX"/> to
/// <see cref="FastiraChartControl.MaximumX"/>).
/// </description></item>
/// <item><description>
/// <see cref="Y"/> — a Y-value reference line, drawn as a <em>horizontal</em> screen line from the left edge to the right,
/// at that Y value on the Y axis (<see cref="FastiraChartControl.MinimumY"/> to <see cref="FastiraChartControl.MaximumY"/>).
/// </description></item>
/// </list>
/// <para>
/// So <c>new ChartReferenceLine(ReferenceLineAxis.X, 42)</c> reads "at X = 42". The values are numbered explicitly so
/// they stay stable when stored.
/// </para>
/// </remarks>
public enum ReferenceLineAxis
{
    /// <summary>
    /// An X-value reference line: the value is on the X axis, and the line is a vertical screen line. The default.
    /// </summary>
    X = 0,

    /// <summary>
    /// A Y-value reference line: the value is on the Y axis, and the line is a horizontal screen line.
    /// </summary>
    Y = 1,
}
