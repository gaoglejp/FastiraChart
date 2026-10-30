using System.Globalization;

namespace FastiraChart.Rendering;

/// <summary>
/// How much data the series builds walked, and which gestures changed the chart.
/// </summary>
/// <remarks>
/// <para>
/// Internal instrumentation, kept apart from <see cref="RenderCounters"/>, <see cref="CacheCounters"/>,
/// <see cref="LensCounters"/> and <see cref="InteractionCounters"/>, so every earlier reading means exactly what it meant.
/// It adds two quantities no other counter holds: the points a build walked, which
/// a build count alone cannot tell apart at 100 points and at 100 000, and the gesture count split by kind.
/// </para>
/// <para>
/// <b>Points placed</b> are added once per series geometry build, by that series' point count — one addition per build,
/// never one per point. A build that reused its geometry adds nothing.
/// </para>
/// <para>
/// <b>Gestures</b> are counted where the control's gesture count is counted, and only there: a wheel step or a drag position
/// that wrote a range or the lens. A step at a zoom limit, a drag position that moved nothing, or a gesture that is off
/// counts nothing. The control's total is their sum.
/// </para>
/// <para>
/// Every count only ever grows; internal validation subtracts two readings. Internal: it is not a public API, and counting has
/// no effect on what is drawn.
/// </para>
/// </remarks>
/// <param name="SeriesPointsPlaced">Points walked by main-plot series geometry builds.</param>
/// <param name="LensSeriesPointsPlaced">Points walked by lens series geometry builds.</param>
/// <param name="Zooms">Wheel steps that changed a range or the lens source: main X, main Y and lens zooms.</param>
/// <param name="Pans">Pan drag positions that changed a main range.</param>
/// <param name="LensDrags">Lens drag positions that moved the lens or its source.</param>
internal record struct ProcessingCounters(
    long SeriesPointsPlaced,
    long LensSeriesPointsPlaced,
    int Zooms,
    int Pans,
    int LensDrags)
{
    /// <summary>Label texts shaped anew by a label rebuild — main axes and lens, counted per label.</summary>
    public int LabelTextsShaped { get; set; }

    /// <summary>Label texts a label rebuild took, already shaped, from the same axis's (or the lens's) last build, counted per label.</summary>
    public int LabelTextsReused { get; set; }

    /// <summary>Gestures that changed state, of every kind: the control's gesture count.</summary>
    public readonly int Gestures => Zooms + Pans + LensDrags;

    /// <summary>What happened between two readings.</summary>
    public static ProcessingCounters operator -(ProcessingCounters a, ProcessingCounters b) => new(
        a.SeriesPointsPlaced - b.SeriesPointsPlaced,
        a.LensSeriesPointsPlaced - b.LensSeriesPointsPlaced,
        a.Zooms - b.Zooms,
        a.Pans - b.Pans,
        a.LensDrags - b.LensDrags)
    {
        LabelTextsShaped = a.LabelTextsShaped - b.LabelTextsShaped,
        LabelTextsReused = a.LabelTextsReused - b.LabelTextsReused,
    };

    /// <summary>The counts on one line, for a report.</summary>
    public readonly string Describe()
        => string.Create(CultureInfo.InvariantCulture, $"points placed {SeriesPointsPlaced} (lens {LensSeriesPointsPlaced}); zooms {Zooms}, pans {Pans}, lens drags {LensDrags}; label texts shaped {LabelTextsShaped}, reused {LabelTextsReused}");
}
