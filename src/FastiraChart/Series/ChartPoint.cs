namespace FastiraChart.Series;

/// <summary>
/// One data point: an X value and a Y value, in data units.
/// </summary>
/// <param name="X">The X value, placed on the X axis (<see cref="FastiraChartControl.MinimumX"/> to <see cref="FastiraChartControl.MaximumX"/>).</param>
/// <param name="Y">The Y value, placed on the Y axis (<see cref="FastiraChartControl.MinimumY"/> to <see cref="FastiraChartControl.MaximumY"/>).</param>
/// <remarks>
/// <para>
/// A readonly value type of two doubles and nothing else, so a snapshot of ten thousand points is one array
/// of twenty thousand doubles — no object per point, nothing for the garbage collector to trace. Styling,
/// identity and change notification belong to the series that holds the points, not to a point.
/// </para>
/// <para>
/// <b>Any double is accepted</b>, NaN and infinity included. A point that is not finite has no position on
/// any scale, and the series draws it as a gap: the line stops before it and starts again after it. It is
/// never thrown on, so one bad sample cannot bring down a chart of many good ones.
/// </para>
/// <para>
/// A record struct: two points are equal when both values are (<see cref="double.Equals(double)"/>, so NaN equals NaN),
/// it deconstructs as <c>var (x, y) = point</c>, and <see cref="object.ToString"/> formats with the current culture, for
/// debugging only.
/// </para>
/// </remarks>
public readonly record struct ChartPoint(double X, double Y);
