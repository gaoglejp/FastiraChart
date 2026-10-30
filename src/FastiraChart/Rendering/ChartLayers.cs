namespace FastiraChart.Rendering;

/// <summary>
/// The drawing layers a change can make stale, and the layout.
/// </summary>
/// <remarks>
/// <para>
/// The dirty model. Every input the control reads is assigned to the layers that draw from it, and a
/// change to that input marks those layers stale and no others: a grid brush marks the grid, an append marks the
/// series, a reference value marks the reference layer. A stale layer is built once, when WPF next renders it,
/// however many changes came before; see <see cref="DeferredLayer{TFrame}"/>.
/// </para>
/// <para>
/// <see cref="Layout"/> is the one flag that is not a layer. It asks WPF for an arrange of the control, because the
/// change may move the plot. The arrange then decides which layers that made stale, by comparing the plot and the
/// axis bands it computed with the ones the layers were last placed against.
/// </para>
/// <para>
/// A change also says which value axis it reaches (<see cref="ValueAxes"/>): a horizontal range marks
/// the same layers a vertical one does, but inside each only the geometry drawn from the horizontal axis is placed
/// again.
/// </para>
/// <para>
/// Internal: the dirty model is an implementation detail, not a public API.
/// </para>
/// </remarks>
[Flags]
internal enum ChartLayers
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>The major grid: <c>PART_GridHost</c>.</summary>
    Grid = 1 << 0,

    /// <summary>The reference lines and both origin lines: <c>PART_ReferenceLineHost</c>.</summary>
    Reference = 1 << 1,

    /// <summary>Every series: <c>PART_SeriesHost</c>.</summary>
    Series = 1 << 2,

    /// <summary>The tick marks and the labels: the two layers in <c>PART_AxisHost</c>.</summary>
    Axis = 1 << 3,

    /// <summary>The control's arrange, which can move the plot under every layer above.</summary>
    Layout = 1 << 4,

    /// <summary>The lens overlay: <c>PART_LensHost</c>.</summary>
    /// <remarks>
    /// Marked by a change to the lens state alone, which marks no other layer; and, as one more layer placed against the
    /// plot from the main ranges, by everything that marks <see cref="Geometry"/>.
    /// </remarks>
    Lens = 1 << 5,

    /// <summary>Every layer whose geometry is placed against the plot rectangle.</summary>
    /// <remarks>The lens is among them: it is placed against the plot and the main ranges.</remarks>
    Geometry = Grid | Reference | Series | Axis | Lens,
}
