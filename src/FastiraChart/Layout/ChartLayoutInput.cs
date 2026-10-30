using System.Windows;

namespace FastiraChart.Layout;

/// <summary>
/// The complete input to a plot layout calculation.
/// </summary>
/// <param name="AvailableSize">
/// The area available for padding, axis space and the plot, in device-independent pixels.
/// This is the area already inside the control's border, so border thickness must not be
/// subtracted again.
/// </param>
/// <param name="RequestedPadding">
/// The padding the consumer asked for, exactly as set on the control. It is never
/// modified; the calculation produces a separate effective padding instead.
/// </param>
/// <param name="MinimumPlotWidth">Absolute minimum plot width, in DIPs.</param>
/// <param name="MinimumPlotHeight">Absolute minimum plot height, in DIPs.</param>
/// <param name="MinimumPlotRatio">
/// Minimum fraction of <paramref name="AvailableSize"/> to keep for the plot, in the
/// range 0 to 1 inclusive.
/// </param>
/// <param name="RequestedAxisSpace">
/// The space requested per edge for axis content — ticks, labels and titles — in DIPs.
/// Like the padding it is never modified; the calculation produces a separate effective
/// axis space. Defaults to zero on every edge, which reproduces the padding-only layout exactly.
/// </param>
/// <remarks>
/// <para>
/// This is the entire input to the calculation. Nothing else — no previous size, no
/// stored state, no ambient value — takes part, which is what makes the result
/// deterministic: the same input always yields the same <see cref="ChartLayoutResult"/>.
/// </para>
/// <para>
/// <paramref name="MinimumPlotRatio"/> is deliberately a fraction of
/// <paramref name="AvailableSize"/> — the whole area inside the border — and *not* of
/// whatever remains after axis space and padding have been taken. Measuring it against
/// the remainder would make the floor depend on the very values it is meant to constrain,
/// and would silently change the padding-only result as soon as a non-zero axis space appeared.
/// </para>
/// <para>
/// <paramref name="RequestedAxisSpace"/> carries a default so that every padding-only call site
/// keeps computing the same numbers. Axis space is
/// an addition to the layout, not a redefinition of it.
/// </para>
/// </remarks>
internal readonly record struct ChartLayoutInput(
    Size AvailableSize,
    Thickness RequestedPadding,
    double MinimumPlotWidth,
    double MinimumPlotHeight,
    double MinimumPlotRatio,
    Thickness RequestedAxisSpace = default);
