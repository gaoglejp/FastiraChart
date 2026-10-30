using System.Windows;

namespace FastiraChart.Layout;

/// <summary>
/// The result of a plot layout calculation.
/// </summary>
/// <param name="PlotBounds">
/// The plot area, in device-independent pixels, relative to the origin of the area
/// inside the control's border. Always finite and never negative in width or height.
/// </param>
/// <param name="EffectivePadding">
/// The padding actually applied. It equals the requested padding unless it had to be
/// reduced to honour the minimum plot constraints.
/// </param>
/// <param name="EffectiveAxisSpace">
/// The axis space actually granted per edge. It equals the requested axis space unless
/// the available area could not hold it once the minimum plot size was honoured and the
/// padding had already been reduced to zero.
/// </param>
/// <remarks>
/// <para>
/// <see cref="PlotBounds"/> is the single source of truth for where the plot lives.
/// The same rectangle is deliberately not stored anywhere else.
/// </para>
/// <para>
/// The three values are consistent by construction: on each axis the effective padding,
/// the effective axis space and the plot extent add back to the available extent. Nothing
/// is left unaccounted for, and no edge is derived twice.
/// </para>
/// <para>
/// Axis space is reported as a <see cref="Thickness"/>, not as four band rectangles. The
/// bands are derivable from these values, and the layout itself has no tick, label or title geometry to
/// place inside them; the axis layers place that content from these values. Grid, origin, series, lens and overlay bounds remain out of scope entirely.
/// </para>
/// </remarks>
internal readonly record struct ChartLayoutResult(
    Rect PlotBounds,
    Thickness EffectivePadding,
    Thickness EffectiveAxisSpace);
