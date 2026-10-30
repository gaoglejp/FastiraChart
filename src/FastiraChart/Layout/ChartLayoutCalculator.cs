using System.Windows;

namespace FastiraChart.Layout;

/// <summary>
/// Computes the plot area from the available space, the requested axis space, the
/// requested padding and the minimum plot constraints.
/// </summary>
/// <remarks>
/// <para>
/// The calculation is pure: it reads only its <see cref="ChartLayoutInput"/> and keeps
/// no state between calls, so the same input always produces the same result. There is
/// no dependence on a previous size, which is what lets the plot stay visually stable
/// across a resize.
/// </para>
/// <para>
/// Constraint priority, highest first:
/// </para>
/// <list type="number">
///   <item><description>The space actually available.</description></item>
///   <item><description>The minimum plot size.</description></item>
///   <item><description>The requested axis space.</description></item>
///   <item><description>The requested padding.</description></item>
/// </list>
/// <para>
/// So when the area cannot hold everything, padding is reduced first; axis space is only
/// reduced once padding has reached zero and the shortfall remains. Neither requested
/// value is modified — the effective values are reported separately.
/// </para>
/// <para>
/// The minimum plot size is measured against the whole available area, before axis space
/// and padding are taken from it. That is what makes axis space a pure addition: with an
/// axis space of zero the arithmetic reduces exactly to the padding-only layout, including in floating
/// point. Had the ratio been measured against the post-axis remainder, the floor would
/// shrink as axis space grew and every padding-only result would move.
/// </para>
/// <para>
/// Geometrically the area is nested outside-in: padding sits against the border, the axis
/// band sits inside the padding, and the plot sits inside the axis band. Axis content is
/// therefore adjacent to the plot it annotates, which is the only arrangement in which
/// ticks can meet their gridlines.
/// </para>
/// </remarks>
internal static class ChartLayoutCalculator
{
    /// <summary>
    /// Computes the plot bounds, the effective padding and the effective axis space for
    /// the given input.
    /// </summary>
    /// <param name="input">The layout input. See <see cref="ChartLayoutInput"/>.</param>
    /// <returns>
    /// A result whose plot bounds are always finite, with non-negative width and height.
    /// </returns>
    public static ChartLayoutResult Calculate(in ChartLayoutInput input)
    {
        // A non-finite or negative available size can reach us from an unconstrained
        // measure pass (infinity) or a degenerate arrange. Treat it as no space at all
        // rather than letting it poison the arithmetic.
        var availableWidth = Sanitize(input.AvailableSize.Width);
        var availableHeight = Sanitize(input.AvailableSize.Height);

        var horizontal = ResolveAxis(
            availableWidth,
            input.RequestedPadding.Left,
            input.RequestedPadding.Right,
            input.RequestedAxisSpace.Left,
            input.RequestedAxisSpace.Right,
            input.MinimumPlotWidth,
            input.MinimumPlotRatio);

        var vertical = ResolveAxis(
            availableHeight,
            input.RequestedPadding.Top,
            input.RequestedPadding.Bottom,
            input.RequestedAxisSpace.Top,
            input.RequestedAxisSpace.Bottom,
            input.MinimumPlotHeight,
            input.MinimumPlotRatio);

        // The plot origin is the padding plus the axis band, in that order: the band lies
        // between the padding and the plot.
        return new ChartLayoutResult(
            new Rect(
                horizontal.PaddingNear + horizontal.AxisNear,
                vertical.PaddingNear + vertical.AxisNear,
                NonNegative(horizontal.Extent),
                NonNegative(vertical.Extent)),
            new Thickness(
                horizontal.PaddingNear,
                vertical.PaddingNear,
                horizontal.PaddingFar,
                vertical.PaddingFar),
            new Thickness(
                horizontal.AxisNear,
                vertical.AxisNear,
                horizontal.AxisFar,
                vertical.AxisFar));
    }

    /// <summary>
    /// How one axis — horizontal or vertical — was divided up.
    /// </summary>
    /// <param name="PaddingNear">Effective padding at the low edge (left or top).</param>
    /// <param name="PaddingFar">Effective padding at the high edge (right or bottom).</param>
    /// <param name="AxisNear">Effective axis space at the low edge.</param>
    /// <param name="AxisFar">Effective axis space at the high edge.</param>
    /// <param name="Extent">The plot extent on this axis.</param>
    /// <remarks>
    /// The five values always add back to the available extent, so nothing is lost or
    /// counted twice.
    /// </remarks>
    private readonly record struct AxisResolution(
        double PaddingNear,
        double PaddingFar,
        double AxisNear,
        double AxisFar,
        double Extent);

    /// <summary>
    /// Resolves one axis — horizontal or vertical — independently of the other.
    /// </summary>
    /// <param name="available">Space available on this axis, finite and non-negative.</param>
    /// <param name="requestedPaddingNear">Requested padding at the low edge.</param>
    /// <param name="requestedPaddingFar">Requested padding at the high edge.</param>
    /// <param name="requestedAxisNear">Requested axis space at the low edge.</param>
    /// <param name="requestedAxisFar">Requested axis space at the high edge.</param>
    /// <param name="minimumAbsolute">Absolute minimum plot extent on this axis.</param>
    /// <param name="minimumRatio">Minimum fraction of <paramref name="available"/> to keep.</param>
    /// <returns>The effective padding and axis space per edge, and the plot extent.</returns>
    /// <remarks>
    /// The two axes never interact: width constraints cannot move the plot vertically.
    /// </remarks>
    private static AxisResolution ResolveAxis(
        double available,
        double requestedPaddingNear,
        double requestedPaddingFar,
        double requestedAxisNear,
        double requestedAxisFar,
        double minimumAbsolute,
        double minimumRatio)
    {
        // Negative or non-finite requests are meaningless here and would inflate the plot
        // beyond the available area, so clamp every edge at zero.
        var paddingNear = Sanitize(requestedPaddingNear);
        var paddingFar = Sanitize(requestedPaddingFar);
        var axisNear = Sanitize(requestedAxisNear);
        var axisFar = Sanitize(requestedAxisFar);

        // Nothing to divide up.
        if (available <= 0)
        {
            return default;
        }

        // The minimum the plot should keep: the larger of the absolute floor and the ratio
        // of what is available, then clamped to what actually exists. Clamping is what
        // makes an impossible minimum degrade gracefully instead of overflowing.
        //
        // `available` here is the whole area inside the border. See the type remarks for
        // why the ratio is deliberately not taken against the post-axis remainder.
        var required = Math.Max(
            Sanitize(minimumAbsolute),
            available * minimumRatio);

        if (required > available)
        {
            required = available;
        }

        // What is left for axis space and padding once the plot has its minimum.
        var budget = available - required;

        if (budget < 0)
        {
            budget = 0;
        }

        var axisTotal = axisNear + axisFar;
        var paddingTotal = paddingNear + paddingFar;

        if (axisTotal + paddingTotal <= budget)
        {
            // Everything fits. Both requests are honoured exactly and the plot takes the
            // whole remainder, which may well exceed the minimum.
            return new AxisResolution(
                paddingNear,
                paddingFar,
                axisNear,
                axisFar,
                available - axisTotal - paddingTotal);
        }

        if (axisTotal >= budget)
        {
            // Axis space alone fills or overruns the budget, so padding — the lowest
            // priority — collapses entirely and the axis space itself must be reduced.
            if (axisTotal <= 0)
            {
                // Reachable only with a zero budget and no axis space requested: there is
                // nothing to scale and nothing to shrink.
                return new AxisResolution(0, 0, 0, 0, available);
            }

            var axisScale = budget / axisTotal;
            var scaledAxisNear = axisNear * axisScale;

            // Derive the far edge by subtraction rather than scaling it independently, so
            // that the edges add back to exactly `budget`. Deriving it this way keeps the
            // arithmetic self-consistent instead of letting rounding accumulate into a
            // drifting, jittering plot edge.
            var scaledAxisFar = budget - scaledAxisNear;

            if (scaledAxisFar < 0)
            {
                scaledAxisFar = 0;
            }

            return new AxisResolution(0, 0, scaledAxisNear, scaledAxisFar, available - budget);
        }

        // Axis space fits; only the padding has to shrink, into whatever the axis space
        // left behind. Scale both padding edges by the same factor so their ratio is
        // preserved and an asymmetric padding does not jump towards centre.
        var paddingBudget = budget - axisTotal;

        if (paddingTotal <= 0)
        {
            // Unreachable by construction: with no padding requested the first branch's
            // test reduces to `axisTotal <= budget`, which this branch has already ruled
            // in. Kept as a guard because what follows divides by paddingTotal, and a
            // division by zero should not depend on that reasoning staying true.
            return new AxisResolution(0, 0, axisNear, axisFar, available - axisTotal);
        }

        var paddingScale = paddingBudget / paddingTotal;
        var scaledPaddingNear = paddingNear * paddingScale;
        var scaledPaddingFar = paddingBudget - scaledPaddingNear;

        if (scaledPaddingFar < 0)
        {
            scaledPaddingFar = 0;
        }

        return new AxisResolution(
            scaledPaddingNear,
            scaledPaddingFar,
            axisNear,
            axisFar,
            available - budget);
    }

    /// <summary>
    /// Clamps a computed plot extent at zero.
    /// </summary>
    /// <remarks>
    /// The arithmetic cannot produce a meaningfully negative extent: every branch derives
    /// the plot from a budget it has already been shown to fit inside. It can, however,
    /// land a fraction of an ULP below zero, because subtracting two separately rounded
    /// sums is not exact — a requested total that just barely exceeds the available extent
    /// can round down to it, leaving the difference very slightly negative.
    /// <see cref="Rect"/> throws on a negative extent, so a value like -1e-16 would be a
    /// crash rather than a rounding artefact. Clamping here makes "finite, non-negative,
    /// never throws" unconditional instead of merely unobserved.
    /// </remarks>
    private static double NonNegative(double value) => value > 0 ? value : 0;

    /// <summary>
    /// Maps NaN, infinity and negative values onto zero.
    /// </summary>
    /// <remarks>
    /// Guards the arithmetic so that a degenerate measure pass — an infinite available
    /// size, say — cannot produce NaN or negative plot bounds downstream.
    /// </remarks>
    private static double Sanitize(double value)
        => double.IsFinite(value) && value > 0 ? value : 0;
}
