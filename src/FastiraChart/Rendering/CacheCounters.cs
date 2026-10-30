using System.Globalization;

namespace FastiraChart.Rendering;

/// <summary>
/// How often each layer's cached geometry and style were reused, and how often they had to be made again.
/// </summary>
/// <remarks>
/// <para>
/// Internal instrumentation, kept beside <see cref="RenderCounters"/>. That one counts how
/// often a layer was <em>composed</em> — its frame put together for a render. This one counts what each
/// composition cost: a <b>build</b> is a part placed or made again from the current state, a <b>hit</b> is a part
/// taken unchanged from the last composition because nothing it is drawn from changed since. A grid brush change
/// composes the grid once and builds its pen and reuses its lines: one style build, one geometry
/// hit.
/// </para>
/// <para>
/// <b>What a part is.</b> For the grid, the ticks and the labels the whole layer's geometry is one part. For the
/// reference lines and the series each item is one part, so one value change among five hundred reference lines is
/// one reference geometry build and 499 hits; the two origin lines together are one part of their own. A style part
/// is a pen (for the labels, the brush handed to the measured text), counted per item where items have their own.
/// Label measurement — shaping the text — and the outward reach of the labels, which the layout reads, are parts of
/// their own.
/// </para>
/// <para>
/// <b>X and Y sub-parts</b>. The grid, the origins, the ticks, the label placements, the measurement and
/// the reach are each kept as two parts, one per value axis (<see cref="ValueAxes"/>), and the reference
/// lines are counted by the axis they mark. The <c>…X…</c> and <c>…Y…</c> counts say which half was made again. The
/// layer-wide counts keep their meaning: a layer part is <em>built</em> in a composition that made either half
/// again, and a <em>hit</em> in one that made neither.
/// </para>
/// <para>
/// Every count only ever grows; internal validation subtracts two readings. Internal: it is not a public API, and counting
/// has no effect on what is drawn.
/// </para>
/// </remarks>
internal record struct CacheCounters(
    int GridGeometryBuilds,
    int GridGeometryHits,
    int GridStyleBuilds,
    int GridStyleHits,
    int ReferenceGeometryBuilds,
    int ReferenceGeometryHits,
    int ReferenceStyleBuilds,
    int ReferenceStyleHits,
    int OriginGeometryBuilds,
    int OriginGeometryHits,
    int OriginStyleBuilds,
    int OriginStyleHits,
    int SeriesGeometryBuilds,
    int SeriesGeometryHits,
    int SeriesStyleBuilds,
    int SeriesStyleHits,
    int TickGeometryBuilds,
    int TickGeometryHits,
    int TickStyleBuilds,
    int TickStyleHits,
    int LabelMeasurements,
    int LabelMeasurementHits,
    int LabelGeometryBuilds,
    int LabelGeometryHits,
    int LabelStyleBuilds,
    int LabelStyleHits,
    int LabelExtentBuilds,
    int LabelExtentHits,
    int GridXGeometryBuilds,
    int GridXGeometryHits,
    int GridYGeometryBuilds,
    int GridYGeometryHits,
    int ReferenceXGeometryBuilds,
    int ReferenceXGeometryHits,
    int ReferenceYGeometryBuilds,
    int ReferenceYGeometryHits,
    int OriginXGeometryBuilds,
    int OriginXGeometryHits,
    int OriginYGeometryBuilds,
    int OriginYGeometryHits,
    int TickXGeometryBuilds,
    int TickXGeometryHits,
    int TickYGeometryBuilds,
    int TickYGeometryHits,
    int LabelXGeometryBuilds,
    int LabelXGeometryHits,
    int LabelYGeometryBuilds,
    int LabelYGeometryHits,
    int LabelXMeasurements,
    int LabelXMeasurementHits,
    int LabelYMeasurements,
    int LabelYMeasurementHits,
    int LabelXExtentBuilds,
    int LabelXExtentHits,
    int LabelYExtentBuilds,
    int LabelYExtentHits)
{
    /// <summary>Geometry made again, on every layer: placed coordinates, measured text and the labels' reach.</summary>
    public readonly int GeometryBuilds
        => GridGeometryBuilds + ReferenceGeometryBuilds + OriginGeometryBuilds + SeriesGeometryBuilds
        + TickGeometryBuilds + LabelGeometryBuilds + LabelMeasurements + LabelExtentBuilds;

    /// <summary>Styles made again, on every layer.</summary>
    public readonly int StyleBuilds
        => GridStyleBuilds + ReferenceStyleBuilds + OriginStyleBuilds + SeriesStyleBuilds + TickStyleBuilds + LabelStyleBuilds;

    /// <summary>
    /// Geometry drawn from the horizontal value axis (X) made again: vertical grid lines, X reference lines,
    /// the vertical origin, the bottom or top axis's ticks, label placements, measurement and reach. Series excluded:
    /// a series is drawn from both axes.
    /// </summary>
    public readonly int XGeometryBuilds
        => GridXGeometryBuilds + ReferenceXGeometryBuilds + OriginXGeometryBuilds + TickXGeometryBuilds
        + LabelXGeometryBuilds + LabelXMeasurements + LabelXExtentBuilds;

    /// <summary>
    /// Geometry drawn from the vertical value axis (Y) made again: horizontal grid lines, Y reference lines,
    /// the horizontal origin, the left or right axis's ticks, label placements, measurement and reach.
    /// </summary>
    public readonly int YGeometryBuilds
        => GridYGeometryBuilds + ReferenceYGeometryBuilds + OriginYGeometryBuilds + TickYGeometryBuilds
        + LabelYGeometryBuilds + LabelYMeasurements + LabelYExtentBuilds;

    /// <summary>What happened between two readings.</summary>
    public static CacheCounters operator -(CacheCounters a, CacheCounters b) => new(
        a.GridGeometryBuilds - b.GridGeometryBuilds,
        a.GridGeometryHits - b.GridGeometryHits,
        a.GridStyleBuilds - b.GridStyleBuilds,
        a.GridStyleHits - b.GridStyleHits,
        a.ReferenceGeometryBuilds - b.ReferenceGeometryBuilds,
        a.ReferenceGeometryHits - b.ReferenceGeometryHits,
        a.ReferenceStyleBuilds - b.ReferenceStyleBuilds,
        a.ReferenceStyleHits - b.ReferenceStyleHits,
        a.OriginGeometryBuilds - b.OriginGeometryBuilds,
        a.OriginGeometryHits - b.OriginGeometryHits,
        a.OriginStyleBuilds - b.OriginStyleBuilds,
        a.OriginStyleHits - b.OriginStyleHits,
        a.SeriesGeometryBuilds - b.SeriesGeometryBuilds,
        a.SeriesGeometryHits - b.SeriesGeometryHits,
        a.SeriesStyleBuilds - b.SeriesStyleBuilds,
        a.SeriesStyleHits - b.SeriesStyleHits,
        a.TickGeometryBuilds - b.TickGeometryBuilds,
        a.TickGeometryHits - b.TickGeometryHits,
        a.TickStyleBuilds - b.TickStyleBuilds,
        a.TickStyleHits - b.TickStyleHits,
        a.LabelMeasurements - b.LabelMeasurements,
        a.LabelMeasurementHits - b.LabelMeasurementHits,
        a.LabelGeometryBuilds - b.LabelGeometryBuilds,
        a.LabelGeometryHits - b.LabelGeometryHits,
        a.LabelStyleBuilds - b.LabelStyleBuilds,
        a.LabelStyleHits - b.LabelStyleHits,
        a.LabelExtentBuilds - b.LabelExtentBuilds,
        a.LabelExtentHits - b.LabelExtentHits,
        a.GridXGeometryBuilds - b.GridXGeometryBuilds,
        a.GridXGeometryHits - b.GridXGeometryHits,
        a.GridYGeometryBuilds - b.GridYGeometryBuilds,
        a.GridYGeometryHits - b.GridYGeometryHits,
        a.ReferenceXGeometryBuilds - b.ReferenceXGeometryBuilds,
        a.ReferenceXGeometryHits - b.ReferenceXGeometryHits,
        a.ReferenceYGeometryBuilds - b.ReferenceYGeometryBuilds,
        a.ReferenceYGeometryHits - b.ReferenceYGeometryHits,
        a.OriginXGeometryBuilds - b.OriginXGeometryBuilds,
        a.OriginXGeometryHits - b.OriginXGeometryHits,
        a.OriginYGeometryBuilds - b.OriginYGeometryBuilds,
        a.OriginYGeometryHits - b.OriginYGeometryHits,
        a.TickXGeometryBuilds - b.TickXGeometryBuilds,
        a.TickXGeometryHits - b.TickXGeometryHits,
        a.TickYGeometryBuilds - b.TickYGeometryBuilds,
        a.TickYGeometryHits - b.TickYGeometryHits,
        a.LabelXGeometryBuilds - b.LabelXGeometryBuilds,
        a.LabelXGeometryHits - b.LabelXGeometryHits,
        a.LabelYGeometryBuilds - b.LabelYGeometryBuilds,
        a.LabelYGeometryHits - b.LabelYGeometryHits,
        a.LabelXMeasurements - b.LabelXMeasurements,
        a.LabelXMeasurementHits - b.LabelXMeasurementHits,
        a.LabelYMeasurements - b.LabelYMeasurements,
        a.LabelYMeasurementHits - b.LabelYMeasurementHits,
        a.LabelXExtentBuilds - b.LabelXExtentBuilds,
        a.LabelXExtentHits - b.LabelXExtentHits,
        a.LabelYExtentBuilds - b.LabelYExtentBuilds,
        a.LabelYExtentHits - b.LabelYExtentHits);

    /// <summary>The sum of two stretches.</summary>
    public static CacheCounters operator +(CacheCounters a, CacheCounters b) => new(
        a.GridGeometryBuilds + b.GridGeometryBuilds,
        a.GridGeometryHits + b.GridGeometryHits,
        a.GridStyleBuilds + b.GridStyleBuilds,
        a.GridStyleHits + b.GridStyleHits,
        a.ReferenceGeometryBuilds + b.ReferenceGeometryBuilds,
        a.ReferenceGeometryHits + b.ReferenceGeometryHits,
        a.ReferenceStyleBuilds + b.ReferenceStyleBuilds,
        a.ReferenceStyleHits + b.ReferenceStyleHits,
        a.OriginGeometryBuilds + b.OriginGeometryBuilds,
        a.OriginGeometryHits + b.OriginGeometryHits,
        a.OriginStyleBuilds + b.OriginStyleBuilds,
        a.OriginStyleHits + b.OriginStyleHits,
        a.SeriesGeometryBuilds + b.SeriesGeometryBuilds,
        a.SeriesGeometryHits + b.SeriesGeometryHits,
        a.SeriesStyleBuilds + b.SeriesStyleBuilds,
        a.SeriesStyleHits + b.SeriesStyleHits,
        a.TickGeometryBuilds + b.TickGeometryBuilds,
        a.TickGeometryHits + b.TickGeometryHits,
        a.TickStyleBuilds + b.TickStyleBuilds,
        a.TickStyleHits + b.TickStyleHits,
        a.LabelMeasurements + b.LabelMeasurements,
        a.LabelMeasurementHits + b.LabelMeasurementHits,
        a.LabelGeometryBuilds + b.LabelGeometryBuilds,
        a.LabelGeometryHits + b.LabelGeometryHits,
        a.LabelStyleBuilds + b.LabelStyleBuilds,
        a.LabelStyleHits + b.LabelStyleHits,
        a.LabelExtentBuilds + b.LabelExtentBuilds,
        a.LabelExtentHits + b.LabelExtentHits,
        a.GridXGeometryBuilds + b.GridXGeometryBuilds,
        a.GridXGeometryHits + b.GridXGeometryHits,
        a.GridYGeometryBuilds + b.GridYGeometryBuilds,
        a.GridYGeometryHits + b.GridYGeometryHits,
        a.ReferenceXGeometryBuilds + b.ReferenceXGeometryBuilds,
        a.ReferenceXGeometryHits + b.ReferenceXGeometryHits,
        a.ReferenceYGeometryBuilds + b.ReferenceYGeometryBuilds,
        a.ReferenceYGeometryHits + b.ReferenceYGeometryHits,
        a.OriginXGeometryBuilds + b.OriginXGeometryBuilds,
        a.OriginXGeometryHits + b.OriginXGeometryHits,
        a.OriginYGeometryBuilds + b.OriginYGeometryBuilds,
        a.OriginYGeometryHits + b.OriginYGeometryHits,
        a.TickXGeometryBuilds + b.TickXGeometryBuilds,
        a.TickXGeometryHits + b.TickXGeometryHits,
        a.TickYGeometryBuilds + b.TickYGeometryBuilds,
        a.TickYGeometryHits + b.TickYGeometryHits,
        a.LabelXGeometryBuilds + b.LabelXGeometryBuilds,
        a.LabelXGeometryHits + b.LabelXGeometryHits,
        a.LabelYGeometryBuilds + b.LabelYGeometryBuilds,
        a.LabelYGeometryHits + b.LabelYGeometryHits,
        a.LabelXMeasurements + b.LabelXMeasurements,
        a.LabelXMeasurementHits + b.LabelXMeasurementHits,
        a.LabelYMeasurements + b.LabelYMeasurements,
        a.LabelYMeasurementHits + b.LabelYMeasurementHits,
        a.LabelXExtentBuilds + b.LabelXExtentBuilds,
        a.LabelXExtentHits + b.LabelXExtentHits,
        a.LabelYExtentBuilds + b.LabelYExtentBuilds,
        a.LabelYExtentHits + b.LabelYExtentHits);

    /// <summary>Builds and hits per layer on one line, for a report.</summary>
    public readonly string Describe()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"geometry/style builds (hits): grid {GridGeometryBuilds}({GridGeometryHits})/{GridStyleBuilds}({GridStyleHits}) ref {ReferenceGeometryBuilds}({ReferenceGeometryHits})/{ReferenceStyleBuilds}({ReferenceStyleHits}) origin {OriginGeometryBuilds}({OriginGeometryHits})/{OriginStyleBuilds}({OriginStyleHits}) series {SeriesGeometryBuilds}({SeriesGeometryHits})/{SeriesStyleBuilds}({SeriesStyleHits}) tick {TickGeometryBuilds}({TickGeometryHits})/{TickStyleBuilds}({TickStyleHits}) label {LabelGeometryBuilds}({LabelGeometryHits})/{LabelStyleBuilds}({LabelStyleHits}) measure {LabelMeasurements}({LabelMeasurementHits}) reach {LabelExtentBuilds}({LabelExtentHits})");

    /// <summary>The X and Y sub-part builds on one line, for a report.</summary>
    public readonly string DescribeAxes()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"X/Y builds: grid {GridXGeometryBuilds}/{GridYGeometryBuilds} ref {ReferenceXGeometryBuilds}/{ReferenceYGeometryBuilds} origin {OriginXGeometryBuilds}/{OriginYGeometryBuilds} tick {TickXGeometryBuilds}/{TickYGeometryBuilds} label {LabelXGeometryBuilds}/{LabelYGeometryBuilds} measure {LabelXMeasurements}/{LabelYMeasurements} reach {LabelXExtentBuilds}/{LabelYExtentBuilds} series {SeriesGeometryBuilds}");
}
