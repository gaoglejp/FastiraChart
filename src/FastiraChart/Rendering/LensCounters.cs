using System.Globalization;

namespace FastiraChart.Rendering;

/// <summary>
/// How often the lens was invalidated and composed, and how often each part of it was placed again or reused.
/// </summary>
/// <remarks>
/// <para>
/// Internal instrumentation, kept beside <see cref="RenderCounters"/> and <see cref="CacheCounters"/>
/// and apart from both, so no main-layer count includes the lens.
/// </para>
/// <para>
/// A <b>build</b> is one composition of the lens frame. Inside it the horizontal half of the lens mapping — the effective
/// source, the lens rectangle and the source's reciprocal — is one cached part: <b>placed</b> again when the lens state,
/// the plot or the main horizontal range changed, <b>reused</b> otherwise.
/// </para>
/// <para>
/// <b>Content parts</b>, each counted as built or reused per composition, as the main layers are:
/// </para>
/// <list type="bullet">
/// <item>the lens's X ticks (generated over the effective source);</item>
/// <item>the X part — vertical grid lines, the x = 0 origin, the tick marks — drawn from the lens's X mapping;</item>
/// <item>the Y part — horizontal grid lines, the y = 0 origin — drawn from the main Y mapping across the lens's width;</item>
/// <item>each reference line, counted by the axis it marks, and each series;</item>
/// <item>the label text (measurement), the label placements and the label brush (style).</item>
/// </list>
/// <para>
/// Every count only ever grows; internal validation subtracts two readings. Internal: it is not a public API.
/// </para>
/// </remarks>
/// <param name="Builds">Compositions of the lens frame.</param>
/// <param name="Invalidations">Requests to compose it again.</param>
/// <param name="PlacementBuilds">Compositions that placed the lens's horizontal mapping again.</param>
/// <param name="PlacementHits">Compositions that reused it.</param>
internal record struct LensCounters(
    int Builds,
    int Invalidations,
    int PlacementBuilds,
    int PlacementHits)
{
    /// <summary>Lens X tick sequences generated.</summary>
    public int TickGenerations { get; set; }

    /// <summary>X parts placed again: vertical grid lines, x = 0, tick marks.</summary>
    public int XBuilds { get; set; }

    /// <summary>Compositions that reused the X part.</summary>
    public int XHits { get; set; }

    /// <summary>Y parts placed again: horizontal grid lines, y = 0.</summary>
    public int YBuilds { get; set; }

    /// <summary>Compositions that reused the Y part.</summary>
    public int YHits { get; set; }

    /// <summary>X-value reference lines placed again in the lens.</summary>
    public int ReferenceXBuilds { get; set; }

    /// <summary>Y-value reference lines placed again in the lens.</summary>
    public int ReferenceYBuilds { get; set; }

    /// <summary>Reference lines whose lens geometry was reused.</summary>
    public int ReferenceHits { get; set; }

    /// <summary>Series whose lens geometry was built again.</summary>
    public int SeriesBuilds { get; set; }

    /// <summary>Series whose lens geometry was reused.</summary>
    public int SeriesHits { get; set; }

    /// <summary>Times the lens label text was measured again.</summary>
    public int LabelMeasurements { get; set; }

    /// <summary>Compositions that reused the measured lens label text.</summary>
    public int LabelMeasurementHits { get; set; }

    /// <summary>Times the lens labels were placed again.</summary>
    public int LabelPlacementBuilds { get; set; }

    /// <summary>Compositions that reused the lens label placements.</summary>
    public int LabelPlacementHits { get; set; }

    /// <summary>Times the measured lens label text was handed a new brush in place.</summary>
    public int LabelStyleBuilds { get; set; }

    /// <summary>Every content part made again, summed: zero for a composition that reused everything.</summary>
    public readonly int ContentBuilds
        => TickGenerations + XBuilds + YBuilds + ReferenceXBuilds + ReferenceYBuilds + SeriesBuilds + LabelMeasurements + LabelPlacementBuilds + LabelStyleBuilds;

    /// <summary>What happened between two readings.</summary>
    public static LensCounters operator -(LensCounters a, LensCounters b) => new(
        a.Builds - b.Builds,
        a.Invalidations - b.Invalidations,
        a.PlacementBuilds - b.PlacementBuilds,
        a.PlacementHits - b.PlacementHits)
    {
        TickGenerations = a.TickGenerations - b.TickGenerations,
        XBuilds = a.XBuilds - b.XBuilds,
        XHits = a.XHits - b.XHits,
        YBuilds = a.YBuilds - b.YBuilds,
        YHits = a.YHits - b.YHits,
        ReferenceXBuilds = a.ReferenceXBuilds - b.ReferenceXBuilds,
        ReferenceYBuilds = a.ReferenceYBuilds - b.ReferenceYBuilds,
        ReferenceHits = a.ReferenceHits - b.ReferenceHits,
        SeriesBuilds = a.SeriesBuilds - b.SeriesBuilds,
        SeriesHits = a.SeriesHits - b.SeriesHits,
        LabelMeasurements = a.LabelMeasurements - b.LabelMeasurements,
        LabelMeasurementHits = a.LabelMeasurementHits - b.LabelMeasurementHits,
        LabelPlacementBuilds = a.LabelPlacementBuilds - b.LabelPlacementBuilds,
        LabelPlacementHits = a.LabelPlacementHits - b.LabelPlacementHits,
        LabelStyleBuilds = a.LabelStyleBuilds - b.LabelStyleBuilds,
    };

    /// <summary>The counts on one line, for a report.</summary>
    public readonly string Describe()
        => string.Create(CultureInfo.InvariantCulture, $"lens builds {Builds} (placed {PlacementBuilds}, reused {PlacementHits}), invalidations {Invalidations}");

    /// <summary>The content counts on one line, for a report.</summary>
    public readonly string DescribeContent()
        => string.Create(CultureInfo.InvariantCulture, $"ticks {TickGenerations} X {XBuilds}/{XHits} Y {YBuilds}/{YHits} refX {ReferenceXBuilds} refY {ReferenceYBuilds} refHit {ReferenceHits} series {SeriesBuilds}/{SeriesHits} measure {LabelMeasurements}/{LabelMeasurementHits} place {LabelPlacementBuilds}/{LabelPlacementHits} style {LabelStyleBuilds}");
}
