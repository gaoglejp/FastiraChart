using System.Globalization;

namespace FastiraChart.Rendering;

/// <summary>
/// How often the interaction overlay — the crosshair and the tooltip — was invalidated and composed.
/// </summary>
/// <remarks>
/// <para>
/// Internal instrumentation, kept apart from <see cref="RenderCounters"/>, <see cref="CacheCounters"/> and
/// <see cref="LensCounters"/>, so every earlier reading means exactly what it meant: no main or lens count includes the
/// overlay, and a pointer move that composes the overlay still reads as zero main and lens builds.
/// </para>
/// <para>
/// A <b>build</b> is one composition of the overlay frame from the pointer and the chart as they stand. Inside it the
/// tooltip's measured text is one cached part: <b>measured</b> again when its string or its style changed, <b>reused</b>
/// otherwise. Every count only ever grows; internal validation subtracts two readings. Internal: it is not a public API.
/// </para>
/// </remarks>
/// <param name="Builds">Compositions of the overlay frame.</param>
/// <param name="Invalidations">Requests to compose it again.</param>
/// <param name="TextMeasurements">Compositions that measured the tooltip text again.</param>
/// <param name="TextHits">Compositions that reused the measured tooltip text.</param>
internal record struct InteractionCounters(
    int Builds,
    int Invalidations,
    int TextMeasurements,
    int TextHits)
{
    /// <summary>What happened between two readings.</summary>
    public static InteractionCounters operator -(InteractionCounters a, InteractionCounters b) => new(
        a.Builds - b.Builds,
        a.Invalidations - b.Invalidations,
        a.TextMeasurements - b.TextMeasurements,
        a.TextHits - b.TextHits);

    /// <summary>The counts on one line, for a report.</summary>
    public readonly string Describe()
        => string.Create(CultureInfo.InvariantCulture, $"overlay builds {Builds}, invalidations {Invalidations}, text measured {TextMeasurements} / reused {TextHits}");
}
