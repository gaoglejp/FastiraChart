using FastiraChart.Axes;

namespace FastiraChart.Interaction;

/// <summary>
/// The range arithmetic of zoom, pan and lens drag: what a gesture does to a range, and when it refuses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ranges in, ranges out.</b> Nothing here knows of a pointer, a plot or a control. A gesture is resolved to a value — the
/// zoom anchor, read by the pointer inverse — or to a shift in data units, and this turns a range and that value into the new
/// range. The control writes it to the range or lens properties, and the existing dirty model draws it.
/// </para>
/// <para>
/// <b>An invalid range is never produced.</b> Every result is finite, ordered and has a finite reciprocal — the conditions
/// <see cref="LinearAxisMapping.TryGetInverseRange"/> asks for, and one more: the reciprocal itself must be finite, which a
/// subnormal span breaks. A gesture whose result would fail is refused and the range is left as it was.
/// </para>
/// <para>
/// Pure and deterministic: the same inputs give the same doubles.
/// </para>
/// </remarks>
internal static class RangeInteraction
{
    /// <summary>How much one wheel notch zooms: the span is divided by this per notch in, multiplied per notch out.</summary>
    /// <remarks>
    /// 1.2, the step most charting and mapping tools settle on: ten notches are about 6.2×, a hand's flick, and a single
    /// notch is visible without being a jump. Internal: there is no public zoom-step setting.
    /// </remarks>
    internal const double ZoomStep = 1.2;

    /// <summary>The wheel delta of one notch: <c>WHEEL_DELTA</c>.</summary>
    internal const double WheelNotch = 120;

    /// <summary>The fewest distinct doubles a zoom in may leave per DIP of the axis it is drawn across.</summary>
    /// <remarks>
    /// <para>
    /// <b>Precision, not a size.</b> A zoom in stops when the doubles across the range become too few for the plot showing
    /// it: fewer than 16 per DIP of the axis's extent. The limit follows the values' magnitude and the plot's size — at
    /// 0..0.001 on a 1000-DIP plot a span of about 3.5×10⁻¹⁵ is allowed, at 10⁶ one of 1.9×10⁻⁶, at 1.7×10¹² (epoch
    /// milliseconds) one of about 4 — so no fixed data span or DIP size is part of the zoom.
    /// </para>
    /// <para>
    /// <b>Why 16 per DIP.</b> Each end of a zoomed range rounds by at most half an ulp, which moves the anchor by at most
    /// <c>½ · ulp · extent / span</c> = 1/32 DIP at the limit: under a tenth of a device pixel even at 300 %, where every
    /// device pixel still has five values of its own. Further in, the rounding becomes visible as the anchor creeping under a
    /// still pointer, and the tick and label values stop being distinct per pixel.
    /// </para>
    /// <para>
    /// <b>Not a fixed count of doubles.</b> A fixed count of 2^20 doubles would ignore the plot, and would stop a
    /// 1×10¹²..1×10¹²+1000 range after 11 notches at a span of 135, where a double still resolves 1.2×10⁻⁴ — an epoch-millisecond
    /// axis would not zoom below about 256 ms.
    /// </para>
    /// <para>
    /// Only a zoom in is held to it. A range the caller set narrower is kept, and zooming out of it is allowed.
    /// </para>
    /// </remarks>
    internal const double MinimumValuesPerDip = 16;

    /// <summary>The zoom factor of a wheel delta: above 1 zooms in (the wheel turned away from the user), below 1 out.</summary>
    /// <remarks>Fractional deltas, which high-resolution wheels and touchpads send, zoom by the same fraction of a notch.</remarks>
    public static double WheelFactor(double delta) => Math.Pow(ZoomStep, delta / WheelNotch);

    /// <summary>Whether a range can be drawn and mapped: finite, ordered, with a finite span and a finite reciprocal.</summary>
    public static bool IsUsable(double minimum, double maximum)
        => LinearAxisMapping.TryGetInverseRange(minimum, maximum, out var inverse) && double.IsFinite(inverse);

    /// <summary>Whether a range drawn across an extent holds at least <see cref="MinimumValuesPerDip"/> doubles per DIP of it.</summary>
    /// <param name="minimum">The range minimum.</param>
    /// <param name="maximum">The range maximum.</param>
    /// <param name="extent">The DIP the range is drawn across; below 1, taken as 1.</param>
    public static bool HasResolution(double minimum, double maximum, double extent)
        => maximum - minimum >= MinimumValuesPerDip * Math.Max(1, double.IsFinite(extent) ? extent : 1) * Ulp(Math.Max(Math.Abs(minimum), Math.Abs(maximum)));

    /// <summary>
    /// The range zoomed by a factor about an anchor, the anchor staying where it is.
    /// </summary>
    /// <param name="minimum">The range minimum.</param>
    /// <param name="maximum">The range maximum.</param>
    /// <param name="anchor">The value that stays put: the value under the pointer. Within the range.</param>
    /// <param name="factor">Above 1 zooms in, below 1 zooms out.</param>
    /// <param name="extent">The DIP the range is drawn across: the plot's width or height, or the lens's width.</param>
    /// <param name="zoomedMinimum">The new minimum; the old one when the result is false.</param>
    /// <param name="zoomedMaximum">The new maximum; the old one when the result is false.</param>
    /// <returns>
    /// False — and the range unchanged — for an unusable range, factor or anchor; for a zoom in that leaves fewer than
    /// <see cref="MinimumValuesPerDip"/> doubles per DIP of the extent or does not narrow the range; and for a zoom out that
    /// would overflow or does not widen it.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <c>anchor + (end − anchor) / factor</c> for each end. The anchor sits at the same fraction of the new range as of
    /// the old, so it maps to the same coordinate: the pointer keeps the value it was over.
    /// </para>
    /// <para>
    /// <b>Why this form.</b> The distance from the anchor to an end is taken first, and for a range near the anchor's
    /// magnitude — any range zoomed in far, or a large offset — it is exact (Sterbenz); the division and the addition then
    /// round once each, half an ulp of the end. An anchor on an end keeps that end exactly: the distance is 0.
    /// </para>
    /// </remarks>
    public static bool TryZoom(double minimum, double maximum, double anchor, double factor, double extent, out double zoomedMinimum, out double zoomedMaximum)
    {
        zoomedMinimum = minimum;
        zoomedMaximum = maximum;

        if (!double.IsFinite(factor) || !(factor > 0) || factor == 1
            || !IsUsable(minimum, maximum)
            || !double.IsFinite(anchor) || anchor < minimum || anchor > maximum)
        {
            return false;
        }

        var low = anchor + ((minimum - anchor) / factor);
        var high = anchor + ((maximum - anchor) / factor);

        if (factor > 1)
        {
            // Inside the old range, as it is exactly; a distance that was not exact must not round an end outward.
            low = Math.Max(low, minimum);
            high = Math.Min(high, maximum);
        }

        if (!IsUsable(low, high))
        {
            return false;
        }

        var span = maximum - minimum;
        var zoomed = high - low;

        if (factor > 1 ? !(zoomed < span) || !HasResolution(low, high, extent) : !(zoomed > span))
        {
            return false;
        }

        zoomedMinimum = low;
        zoomedMaximum = high;

        return true;
    }

    /// <summary>
    /// The range moved by a distance in data units, its span kept.
    /// </summary>
    /// <param name="minimum">The range minimum.</param>
    /// <param name="maximum">The range maximum.</param>
    /// <param name="shift">How far to move it: positive moves both ends up.</param>
    /// <param name="shiftedMinimum">The new minimum; the old one when the result is false.</param>
    /// <param name="shiftedMaximum">The new maximum; the old one when the result is false.</param>
    /// <returns>False — and the range unchanged — when the range or the shift is unusable, or the result would be.</returns>
    /// <remarks>
    /// <para>
    /// <b>The span is the double <c>maximum − minimum</c>, and it is kept.</b> The new minimum is placed first and the
    /// maximum one span above it, so the span comes back bit-equal whenever the new ends can hold it. Two ends shifted by
    /// one amount do not do this: the smaller end can carry a bit finer than the larger end's grid, which rounds away when
    /// it moves into a coarser binade.
    /// </para>
    /// <para>
    /// The minimum is tried where the shift puts it, then on the larger end's grid, then one span below the maximum put on
    /// that grid; the first for which
    /// <c>(minimum + span) − minimum</c> is the span exactly is taken. Each moves the range by at most one ulp of its larger
    /// end — far below a device pixel, so there is no dead zone and no step. Where no double pair at the new position has
    /// that difference — the span needs a bit finer than both new ends hold — the nearest is taken, off by less than one
    /// ulp of the larger end.
    /// </para>
    /// </remarks>
    public static bool TryShift(double minimum, double maximum, double shift, out double shiftedMinimum, out double shiftedMaximum)
    {
        shiftedMinimum = minimum;
        shiftedMaximum = maximum;

        if (!double.IsFinite(shift) || !IsUsable(minimum, maximum))
        {
            return false;
        }

        if (shift == 0)
        {
            return true;
        }

        var span = maximum - minimum;
        var low = minimum + shift;

        if (!double.IsFinite(low) || !TryPlace(low, span, out var placedMinimum, out var placedMaximum))
        {
            return false;
        }

        shiftedMinimum = placedMinimum;
        shiftedMaximum = placedMaximum;

        return true;
    }

    /// <summary>
    /// A range of a span starting at or a hair from a minimum: bit-equal span where the doubles there can hold it.
    /// </summary>
    private static bool TryPlace(double low, double span, out double minimum, out double maximum)
    {
        minimum = low;
        maximum = low + span;

        var grid = Ulp(Math.Max(Math.Abs(low), Math.Abs(maximum)));

        // On the larger end's grid the sum is exact whenever the span is a multiple of that grid; with the maximum put on
        // that grid and the minimum one span below it, the span needs only the minimum's finer grid — the case of ends in
        // two binades. Powers of two: the divisions, the roundings and the products are exact.
        var snapped = Math.Round(low / grid) * grid;
        var below = (Math.Round(maximum / grid) * grid) - span;

        foreach (var candidate in new[] { low, snapped, below })
        {
            var high = candidate + span;

            if (high - candidate == span && IsUsable(candidate, high))
            {
                minimum = candidate;
                maximum = high;

                return true;
            }
        }

        return IsUsable(minimum, maximum);
    }

    /// <summary>
    /// A lens source moved inside the main range: its span kept, the whole source shifted back in where it would leave.
    /// </summary>
    /// <param name="minimum">The source minimum; inside the main range.</param>
    /// <param name="maximum">The source maximum; inside the main range.</param>
    /// <param name="shift">How far to move it, in X units.</param>
    /// <param name="mainMinimum">The main horizontal minimum.</param>
    /// <param name="mainMaximum">The main horizontal maximum.</param>
    /// <param name="movedMinimum">The new source minimum.</param>
    /// <param name="movedMaximum">The new source maximum.</param>
    /// <returns>False when a range is unusable or the source is not inside the main range.</returns>
    /// <remarks>
    /// The shift is held to <c>[mainMinimum − minimum, mainMaximum − maximum]</c>; at either bound the source stands on the
    /// main edge exactly, with the other end placed one span away. The source is never cut to fit: the lens drag constrains
    /// what it asks for, and the lens's intersection with the main range is not reached.
    /// </remarks>
    public static bool TryShiftInside(double minimum, double maximum, double shift, double mainMinimum, double mainMaximum, out double movedMinimum, out double movedMaximum)
    {
        movedMinimum = minimum;
        movedMaximum = maximum;

        if (!IsUsable(minimum, maximum) || !IsUsable(mainMinimum, mainMaximum) || minimum < mainMinimum || maximum > mainMaximum || !double.IsFinite(shift))
        {
            return false;
        }

        if (!TryShift(minimum, maximum, shift, out var low, out var high))
        {
            return false;
        }

        if (low < mainMinimum)
        {
            return TryFit(mainMinimum, maximum - minimum, mainMinimum, mainMaximum, out movedMinimum, out movedMaximum);
        }

        if (high > mainMaximum)
        {
            return TryFitRight(mainMaximum, maximum - minimum, mainMinimum, mainMaximum, out movedMinimum, out movedMaximum);
        }

        movedMinimum = low;
        movedMaximum = high;

        return true;
    }

    /// <summary>
    /// A source range made to lie inside the main range: kept when it does, shifted in where it pokes out, the main range
    /// itself when it is as wide or wider.
    /// </summary>
    public static bool TryContain(double minimum, double maximum, double mainMinimum, double mainMaximum, out double containedMinimum, out double containedMaximum)
    {
        containedMinimum = minimum;
        containedMaximum = maximum;

        if (!IsUsable(minimum, maximum) || !IsUsable(mainMinimum, mainMaximum))
        {
            return false;
        }

        var span = maximum - minimum;

        if (!(span < mainMaximum - mainMinimum))
        {
            containedMinimum = mainMinimum;
            containedMaximum = mainMaximum;

            return true;
        }

        if (minimum < mainMinimum)
        {
            return TryFit(mainMinimum, span, mainMinimum, mainMaximum, out containedMinimum, out containedMaximum);
        }

        if (maximum > mainMaximum)
        {
            return TryFitRight(mainMaximum, span, mainMinimum, mainMaximum, out containedMinimum, out containedMaximum);
        }

        return true;
    }

    /// <summary>A range of a span starting exactly on the main minimum, its maximum held inside the main range.</summary>
    /// <remarks>The maximum is the double nearest one span up for which the span comes back bit-equal, if one is that near.</remarks>
    private static bool TryFit(double start, double span, double mainMinimum, double mainMaximum, out double minimum, out double maximum)
    {
        minimum = start;
        maximum = Math.Min(Nearest(start + span, high => high - start == span), mainMaximum);

        return IsUsable(minimum, maximum);
    }

    /// <summary>A range of a span ending exactly on the main maximum, its minimum held inside the main range.</summary>
    private static bool TryFitRight(double end, double span, double mainMinimum, double mainMaximum, out double minimum, out double maximum)
    {
        maximum = end;
        minimum = Math.Max(Nearest(end - span, low => end - low == span), mainMinimum);

        return IsUsable(minimum, maximum);
    }

    /// <summary>The double, of a value and its two neighbours, that satisfies a test; the value itself when none does.</summary>
    private static double Nearest(double value, Func<double, bool> exact)
        => exact(value) ? value
        : exact(Math.BitDecrement(value)) ? Math.BitDecrement(value)
        : exact(Math.BitIncrement(value)) ? Math.BitIncrement(value)
        : value;

    /// <summary>The gap from a non-negative magnitude to the next double up.</summary>
    private static double Ulp(double magnitude) => Math.BitIncrement(magnitude) - magnitude;
}
