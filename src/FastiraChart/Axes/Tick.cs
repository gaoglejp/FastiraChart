namespace FastiraChart.Axes;

/// <summary>
/// One major tick: the value it represents and where it sits along its axis.
/// </summary>
/// <param name="Value">
/// The data value, always an exact multiple of the generated step and always within the
/// requested range.
/// </param>
/// <param name="NormalizedPosition">
/// The tick's position along the axis as a fraction of the range, from 0 to 1 inclusive.
/// 0 is the plot's left edge on a horizontal axis and its bottom edge on a vertical one, so
/// values increase left to right and bottom to top.
/// </param>
/// <remarks>
/// <para>
/// Two fields, and deliberately no more. A tick has no label, no font, no measured size and
/// no visual: those belong to the label and mark layers. Carrying a normalized position rather than a
/// coordinate is what keeps generation independent of layout — the same tick sequence is
/// valid at every plot size, and only the mapping to device-independent pixels changes.
/// </para>
/// <para>
/// Internal. A public tick type would fix its shape independently of the labels that
/// constrain it.
/// </para>
/// </remarks>
internal readonly record struct Tick(double Value, double NormalizedPosition);
