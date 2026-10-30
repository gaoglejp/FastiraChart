namespace FastiraChart.Axes;

/// <summary>
/// Generates major ticks for a linear numeric range at "nice" round intervals.
/// </summary>
/// <remarks>
/// <para>
/// Pure and deterministic: the result depends only on the three arguments, and the same
/// arguments always produce the same sequence. No state is kept between calls, so a resize —
/// which changes no argument — cannot change a tick value.
/// </para>
/// <para>
/// Linear numeric ranges only. Date-time, category and logarithmic scales are later work, and
/// no abstraction is built for them here: a scale framework designed before its second
/// implementation exists would be a guess.
/// </para>
/// <para>
/// Major ticks only. There is no minor tick, no subdivision and no label.
/// </para>
/// </remarks>
internal static class LinearTickGenerator
{
    /// <summary>
    /// The mantissas a step is rounded up to, before scaling by a power of ten.
    /// </summary>
    /// <remarks>
    /// 1, 2, 5 and 10 are the intervals people read without effort, because each divides the
    /// decade evenly. 2.5 is sometimes included; it is left out here because it produces
    /// gridlines that read as irregular against the 1/2/5 neighbours on an adjacent axis.
    /// </remarks>
    private static readonly double[] NiceMantissas = [1, 2, 5, 10];

    /// <summary>
    /// The largest number of ticks that will be returned; beyond it the result is empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not reachable through the control, whose target count is validated far below it: the
    /// generated count never exceeds the target by more than one, because the step is rounded
    /// *up* from <c>range / target</c>. The cap exists for the paths validation cannot cover —
    /// a step that underflows to a denormal, or an index computation that overflows — where the
    /// alternative is an allocation of unbounded size.
    /// </para>
    /// <para>
    /// Returning empty rather than truncating is deliberate: a truncated sequence looks like a
    /// valid axis that simply ends early, which is a harder fault to notice than an axis with
    /// no ticks at all.
    /// </para>
    /// </remarks>
    internal const int MaximumTickCount = 10_000;

    /// <summary>
    /// Slack allowed when deciding whether an endpoint falls on a step boundary.
    /// </summary>
    /// <remarks>
    /// Applied to the tick *index*, not the value. A range whose endpoint is mathematically on
    /// a step boundary can compute to an index of 4.999999999999999 or 5.000000000000001, and
    /// without slack the endpoint tick would appear or vanish depending on which way the
    /// division happened to round. The value is generous against representation error and tiny
    /// against a real index.
    /// </remarks>
    private const double IndexTolerance = 1e-9;

    /// <summary>
    /// Generates the major ticks for a range.
    /// </summary>
    /// <param name="minimum">Range minimum.</param>
    /// <param name="maximum">Range maximum, which must be greater than <paramref name="minimum"/>.</param>
    /// <param name="targetTickCount">
    /// How many ticks the caller would like. A target, not a quota: the actual count depends on
    /// where the nice step lands, and is normally within one or two of it.
    /// </param>
    /// <returns>
    /// The ticks in ascending order, every value an exact multiple of the step and within the
    /// range. Empty when no sensible sequence exists — see the remarks.
    /// </returns>
    /// <remarks>
    /// <para>
    /// An empty result is returned, rather than an exception or a fabricated tick, for every
    /// input that has no meaningful answer: a non-finite endpoint, an inverted range, a
    /// degenerate range where minimum equals maximum, a non-positive target, and a step that
    /// cannot be represented.
    /// </para>
    /// <para>
    /// The degenerate case is worth stating plainly. When minimum equals maximum the values
    /// exist but the scale does not: every value maps to the same coordinate, so there is no
    /// position to place a tick at. One tick at the centre would imply a scale that is not
    /// there. The axis line itself remains perfectly drawable.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<Tick> Generate(double minimum, double maximum, int targetTickCount)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum))
        {
            return [];
        }

        // Covers both the inverted range and the degenerate one.
        if (maximum <= minimum)
        {
            return [];
        }

        if (targetTickCount < 1)
        {
            return [];
        }

        var range = maximum - minimum;

        // A range can overflow to infinity even when both endpoints are finite.
        if (!double.IsFinite(range) || range <= 0)
        {
            return [];
        }

        var step = NiceStep(range / targetTickCount);

        if (!double.IsFinite(step) || step <= 0)
        {
            return [];
        }

        // Index arithmetic, not repeated addition: every value is computed as index * step from
        // scratch, so nothing accumulates. Adding the step in a loop drifts by an amount that
        // grows with the tick count, which is exactly where it is least visible.
        var lowIndex = Math.Ceiling((minimum / step) - IndexTolerance);
        var highIndex = Math.Floor((maximum / step) + IndexTolerance);

        if (!double.IsFinite(lowIndex) || !double.IsFinite(highIndex) || highIndex < lowIndex)
        {
            return [];
        }

        var count = highIndex - lowIndex + 1;

        if (count > MaximumTickCount)
        {
            return [];
        }

        var first = (long)lowIndex;
        var last = (long)highIndex;
        var tolerance = step * IndexTolerance;
        var inverseRange = 1.0 / range;
        var ticks = new List<Tick>((int)count);
        var hasPrevious = false;
        var previousValue = 0d;

        for (var index = first; index <= last; index++)
        {
            var value = index * step;

            // The index tolerance can admit an index whose value lands a hair outside the
            // range. Accept it only within the same tolerance, so a genuine endpoint survives
            // representation error while a real out-of-range value does not.
            if (value < minimum - tolerance || value > maximum + tolerance)
            {
                continue;
            }

            // A step can be smaller than the gap between adjacent doubles at the range's
            // magnitude — a range of 1e-6 offset to 1e9 picks a step of 1e-7 where the gap is
            // already 1.19e-7 — and consecutive multiples then round to the same value. Keep
            // only the first of each run, so the sequence is strictly ascending whatever the
            // magnitudes involved.
            //
            // Enforced here in the loop rather than by a precondition on the step. A
            // precondition can be defeated by an input nobody thought to test, which is
            // precisely how this one survived a twenty-thousand-sample random sweep; the
            // invariant holds here by construction instead.
            if (hasPrevious && value <= previousValue)
            {
                continue;
            }

            // The value is kept exactly as index * step — no endpoint is fabricated or snapped —
            // and only the position is clamped, so the guarantee that a normalized position is
            // within 0 to 1 holds without touching the data. The mapping is the shared one, so an
            // origin or reference line at this value reaches the same double.
            ticks.Add(new Tick(value, LinearAxisMapping.Normalize(value, minimum, inverseRange)));

            previousValue = value;
            hasPrevious = true;
        }

        return ticks;
    }

    /// <summary>
    /// Rounds a raw step up to the next 1, 2, 5 or 10 times a power of ten.
    /// </summary>
    /// <remarks>
    /// Rounding up rather than to nearest is what bounds the tick count: the step is never
    /// smaller than <c>range / target</c>, so the count never exceeds the target by more than
    /// the one tick the two endpoints can add.
    /// </remarks>
    private static double NiceStep(double rawStep)
    {
        if (!double.IsFinite(rawStep) || rawStep <= 0)
        {
            return 0;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));

        if (!double.IsFinite(magnitude) || magnitude <= 0)
        {
            return 0;
        }

        var mantissa = rawStep / magnitude;

        foreach (var candidate in NiceMantissas)
        {
            if (mantissa <= candidate)
            {
                return candidate * magnitude;
            }
        }

        // Only reachable if rounding puts the mantissa above 10; the next decade is still nice.
        return 10 * magnitude;
    }
}
