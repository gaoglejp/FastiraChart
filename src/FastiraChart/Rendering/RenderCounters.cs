using System.Globalization;

namespace FastiraChart.Rendering;

/// <summary>
/// How many times each layer was invalidated and built, and how many layout passes the control ran.
/// </summary>
/// <remarks>
/// <para>
/// Internal instrumentation, so that the dirty model is proved by counts rather than
/// asserted: a series append that builds the grid, or a grid brush that re-runs the layout, is a counter that moved
/// when it should not have. Every count only ever grows; internal validation subtracts two readings to see what one change
/// cost.
/// </para>
/// <para>
/// A build is one call of a layer's builder: every line, mark, label or series of that layer placed again from
/// the current state. An invalidation is one request to rebuild, which costs a flag and an
/// <see cref="System.Windows.UIElement.InvalidateVisual"/>; any number of them before a frame cost one build.
/// </para>
/// <para>
/// Internal: it is not a public API, and incrementing it has no effect on what is drawn.
/// </para>
/// </remarks>
/// <param name="GridBuilds">Times the major grid was placed.</param>
/// <param name="ReferenceBuilds">Times the reference and origin lines were placed.</param>
/// <param name="SeriesBuilds">Times every visible series' geometry was built.</param>
/// <param name="TickBuilds">Times the tick marks were placed.</param>
/// <param name="LabelBuilds">Times the labels were placed.</param>
/// <param name="GridInvalidations">Requests to rebuild the grid.</param>
/// <param name="ReferenceInvalidations">Requests to rebuild the reference and origin lines.</param>
/// <param name="SeriesInvalidations">Requests to rebuild the series.</param>
/// <param name="AxisInvalidations">Requests to rebuild the tick marks and labels.</param>
/// <param name="LayoutRequests">Arranges the control asked WPF for itself, because a change may move the plot.</param>
/// <param name="Measures">Times WPF measured the control: <c>MeasureOverride</c> calls.</param>
/// <param name="Arranges">Times WPF arranged the control: <c>ArrangeOverride</c> calls, for any reason.</param>
/// <param name="LayoutChanges">Arranges whose plot or axis bands differed from the last, and so marked layers.</param>
/// <param name="LabelTextBuilds">Times the label text was measured again.</param>
internal record struct RenderCounters(
    int GridBuilds,
    int ReferenceBuilds,
    int SeriesBuilds,
    int TickBuilds,
    int LabelBuilds,
    int GridInvalidations,
    int ReferenceInvalidations,
    int SeriesInvalidations,
    int AxisInvalidations,
    int LayoutRequests,
    int Measures,
    int Arranges,
    int LayoutChanges,
    int LabelTextBuilds)
{
    /// <summary>Builds of either axis layer: the tick marks and the labels.</summary>
    public readonly int AxisBuilds => TickBuilds + LabelBuilds;

    /// <summary>The most builds any one layer had: more than one in a frame is a duplicate.</summary>
    public readonly int MostBuildsOfOneLayer
        => Math.Max(Math.Max(Math.Max(GridBuilds, ReferenceBuilds), Math.Max(SeriesBuilds, TickBuilds)), LabelBuilds);

    /// <summary>Layout passes of the control, measure and arrange together.</summary>
    public readonly int LayoutPasses => Measures + Arranges;

    /// <summary>What happened between two readings.</summary>
    public static RenderCounters operator -(RenderCounters a, RenderCounters b) => new(
        a.GridBuilds - b.GridBuilds,
        a.ReferenceBuilds - b.ReferenceBuilds,
        a.SeriesBuilds - b.SeriesBuilds,
        a.TickBuilds - b.TickBuilds,
        a.LabelBuilds - b.LabelBuilds,
        a.GridInvalidations - b.GridInvalidations,
        a.ReferenceInvalidations - b.ReferenceInvalidations,
        a.SeriesInvalidations - b.SeriesInvalidations,
        a.AxisInvalidations - b.AxisInvalidations,
        a.LayoutRequests - b.LayoutRequests,
        a.Measures - b.Measures,
        a.Arranges - b.Arranges,
        a.LayoutChanges - b.LayoutChanges,
        a.LabelTextBuilds - b.LabelTextBuilds);

    /// <summary>The build and layout counts on one line, for a report.</summary>
    public readonly string Describe()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"builds grid {GridBuilds} ref {ReferenceBuilds} series {SeriesBuilds} tick {TickBuilds} label {LabelBuilds}; measure {Measures} arrange {Arranges} layout-changed {LayoutChanges}");
}
