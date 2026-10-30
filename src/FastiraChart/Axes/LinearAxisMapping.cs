namespace FastiraChart.Axes;

/// <summary>
/// Maps a value on a linear axis to its normalized position along that axis.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one place a value becomes a position.</b> The tick generator uses
/// <see cref="Normalize"/> for every tick it produces, the origin and reference lines
/// use <see cref="TryNormalize"/>, which ends in the same call, and every series point inside
/// the range goes through <see cref="Position"/>, which ends in it too. A grid line, an origin line, a
/// reference line and a series point at the same value therefore pass through one expression and reach
/// one double: they are not four calculations that agree, but one calculation read four times. The normalized
/// position then reaches the screen through <see cref="AxisTickPlacement.HorizontalCoordinate(System.Windows.Rect, double)"/>
/// or <see cref="AxisTickPlacement.VerticalCoordinate(System.Windows.Rect, double)"/>, which are shared
/// in the same way.
/// </para>
/// <para>
/// The arithmetic is the difference from the
/// minimum multiplied by a precomputed <c>1 / (maximum − minimum)</c>, clamped to 0..1 — kept in one place
/// so that it is shared, not rewritten. In particular it multiplies by the reciprocal
/// rather than dividing by the range: the two can differ in the last bit, and a reference line that
/// divided would miss the grid line at the same value by that bit.
/// </para>
/// <para>
/// Pure and deterministic. Linear only, like the generator: no scale abstraction is built ahead of a
/// second scale that would constrain it.
/// </para>
/// </remarks>
internal static class LinearAxisMapping
{
    /// <summary>
    /// The normalized position of a value, given the range minimum and the reciprocal of the range.
    /// </summary>
    /// <param name="value">The value to place.</param>
    /// <param name="minimum">The range minimum.</param>
    /// <param name="inverseRange"><c>1.0 / (maximum − minimum)</c>, computed once by the caller.</param>
    /// <returns>The position from 0 to 1 inclusive; the value itself is never altered.</returns>
    /// <remarks>
    /// Only the position is clamped. A value admitted a hair outside its range — which the tick
    /// generator's index tolerance can do at an endpoint — keeps its exact value and is placed exactly
    /// on the edge, so a normalized position is always within 0 to 1 without touching the data.
    /// </remarks>
    public static double Normalize(double value, double minimum, double inverseRange)
    {
        var normalized = (value - minimum) * inverseRange;

        return normalized < 0 ? 0 : normalized > 1 ? 1 : normalized;
    }

    /// <summary>
    /// The normalized position of a value, if the value lies within a range that has a scale.
    /// </summary>
    /// <param name="value">The value to place.</param>
    /// <param name="minimum">The range minimum.</param>
    /// <param name="maximum">The range maximum.</param>
    /// <param name="normalized">The position from 0 to 1 inclusive, or 0 when the result is false.</param>
    /// <returns>
    /// True when <paramref name="value"/> lies in <c>[minimum, maximum]</c>, endpoints included, and the
    /// range has a scale; false otherwise.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>No clamping of the value.</b> A value outside the range has no position on it, and the answer
    /// is false — not the nearest edge. A line that is clamped to the edge would claim a value the plot
    /// does not show.
    /// </para>
    /// <para>
    /// <b>Endpoints are inside.</b> A value equal to the minimum or the maximum has a position, 0 or 1,
    /// and its line belongs on the plot edge. Being on an edge is not a reason to leave it out.
    /// </para>
    /// <para>
    /// <b>No scale, no position.</b> A non-finite value or endpoint, an inverted range, a degenerate
    /// range (minimum equal to maximum) and a range whose width overflows all return false. These are
    /// the same inputs for which the tick generator returns no ticks, for the same reason: every value
    /// would map to one coordinate, or to none, and a guessed one would imply a scale that is not there.
    /// </para>
    /// <para>
    /// The reciprocal is computed here exactly as the tick generator computes it, so a value that is
    /// also a tick lands on the bit-identical position.
    /// </para>
    /// </remarks>
    public static bool TryNormalize(double value, double minimum, double maximum, out double normalized)
    {
        normalized = 0;

        if (!double.IsFinite(value) || !TryGetInverseRange(minimum, maximum, out var inverseRange))
        {
            return false;
        }

        if (value < minimum || value > maximum)
        {
            return false;
        }

        normalized = Normalize(value, minimum, inverseRange);

        return true;
    }

    /// <summary>
    /// The reciprocal of a range, if the range has a scale.
    /// </summary>
    /// <param name="minimum">The range minimum.</param>
    /// <param name="maximum">The range maximum.</param>
    /// <param name="inverseRange"><c>1.0 / (maximum − minimum)</c>, or 0 when the result is false.</param>
    /// <returns>
    /// False for a non-finite endpoint, an inverted or degenerate range, and a range whose width overflows —
    /// the inputs <see cref="TryNormalize"/> refuses, for the reasons given there.
    /// </returns>
    /// <remarks>
    /// Split out of <see cref="TryNormalize"/>, unchanged, so a series can compute the scale once for
    /// thousands of points and still reach the bit-identical reciprocal the tick generator and the reference
    /// lines use.
    /// </remarks>
    public static bool TryGetInverseRange(double minimum, double maximum, out double inverseRange)
    {
        inverseRange = 0;

        if (!double.IsFinite(minimum) || !double.IsFinite(maximum))
        {
            return false;
        }

        // Covers both the inverted range and the degenerate one.
        if (!(maximum > minimum))
        {
            return false;
        }

        var range = maximum - minimum;

        // A range can overflow to infinity even when both endpoints are finite.
        if (!double.IsFinite(range) || range <= 0)
        {
            return false;
        }

        inverseRange = 1.0 / range;

        return true;
    }

    /// <summary>
    /// The normalized position of any value, inside the range or outside it.
    /// </summary>
    /// <param name="value">The value to place; finite.</param>
    /// <param name="minimum">The range minimum.</param>
    /// <param name="maximum">The range maximum.</param>
    /// <param name="inverseRange">From <see cref="TryGetInverseRange"/> for the same range.</param>
    /// <returns>
    /// Inside <c>[minimum, maximum]</c>, exactly <see cref="Normalize"/>'s answer; below the range, at most 0;
    /// above it, at least 1. May be non-finite for a value far enough outside, which the caller must check.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Why a series needs this and a reference line does not.</b> A reference line outside its range has
    /// nothing to draw. A series point outside the range still anchors the segments to its neighbours, and a
    /// segment between two points on either side of the plot crosses it — so the point is placed where it
    /// lies, off the plot, and the clip decides what shows.
    /// </para>
    /// <para>
    /// <b>One expression, not a second scale.</b> A value inside the range goes through
    /// <see cref="Normalize"/> itself, clamp included, so a series point at a tick's, a grid line's, a
    /// reference's or the origin's value reaches the same double they do — including at an endpoint, where
    /// <c>(maximum − minimum) · (1 / (maximum − minimum))</c> can round a hair past 1 and the clamp is what
    /// makes it exactly 1. Outside the range the same product is used unclamped, held to the correct side of
    /// the edge so that the positions stay in the order of the values.
    /// </para>
    /// </remarks>
    public static double Position(double value, double minimum, double maximum, double inverseRange)
    {
        if (value >= minimum && value <= maximum)
        {
            return Normalize(value, minimum, inverseRange);
        }

        var normalized = (value - minimum) * inverseRange;

        return value < minimum ? Math.Min(normalized, 0) : Math.Max(normalized, 1);
    }

    /// <summary>
    /// The value at a position given as its distance from each end of the range: the inverse of <see cref="Position"/>
    ///.
    /// </summary>
    /// <param name="fromMinimum">The fraction of the range from the minimum — the normalized position; finite.</param>
    /// <param name="fromMaximum">The fraction of the range from the maximum, measured on its own; finite.</param>
    /// <param name="minimum">The range minimum.</param>
    /// <param name="maximum">The range maximum; the range must have a scale (<see cref="TryGetInverseRange"/>).</param>
    /// <returns>
    /// The minimum exactly where <paramref name="fromMinimum"/> is 0, the maximum exactly where <paramref name="fromMaximum"/>
    /// is 0; between them a value within the range; outside it, beyond the nearer end.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>The same meaning backwards.</b> <see cref="Position"/> takes a value to <c>(value − minimum) / (maximum − minimum)</c>;
    /// this takes a position back to <c>minimum + n · (maximum − minimum)</c> — or, from the other end, to
    /// <c>maximum − (1 − n) · (maximum − minimum)</c>, the same number. Nothing else: no snapping to a tick, no rounding to a
    /// pixel.
    /// </para>
    /// <para>
    /// <b>Read from the nearer end, rounded once.</b> The product and the sum are one fused multiply-add, which IEEE 754
    /// rounds once and every machine rounds alike, and the end it starts from is the nearer one, so the product is at most
    /// half the range. Starting from the far end, or rounding the product before the sum, costs more than one ulp of the
    /// coordinate where the range straddles 0 — the terms are larger than the value they cancel to. As it is, a pointer
    /// reads its value to within about one coordinate ulp (<c>ulp(x) · range / extent</c>), which is all a coordinate holds;
    /// each end, and the middle of a range whose middle is representable, come back exactly.
    /// </para>
    /// </remarks>
    public static double Value(double fromMinimum, double fromMaximum, double minimum, double maximum)
    {
        var range = maximum - minimum;

        return fromMinimum <= fromMaximum
            ? Math.FusedMultiplyAdd(fromMinimum, range, minimum)
            : Math.FusedMultiplyAdd(-fromMaximum, range, maximum);
    }
}
