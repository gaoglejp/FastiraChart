namespace FastiraChart.Rendering;

/// <summary>
/// Which value axes a change reaches: the horizontal (X), the vertical (Y), or both.
/// </summary>
/// <remarks>
/// <para>
/// The per-axis refinement of the dirty model. <see cref="ChartLayers"/> says <em>which layers</em> a change makes
/// stale; this says <em>which half of each</em>. Inside every layer the geometry drawn from one value axis is kept
/// apart from the geometry drawn from the other, so a change to one axis's range places again only what that axis
/// reaches and reuses the rest:
/// </para>
/// <list type="table">
/// <listheader><term>Axis</term><description>Reaches</description></listheader>
/// <item>
/// <term><see cref="Horizontal"/> (X)</term>
/// <description>
/// the vertical grid lines, the X-value reference lines and the vertical origin line (x = 0) — every one a
/// <em>vertical</em> screen line — the series' X coordinates, and the bottom or top axis's ticks, label text and label
/// placements.
/// </description>
/// </item>
/// <item>
/// <term><see cref="Vertical"/> (Y)</term>
/// <description>
/// the horizontal grid lines, the Y-value reference lines and the horizontal origin line (y = 0) — every one a
/// <em>horizontal</em> screen line — the series' Y coordinates, and the left or right axis's ticks, label text and
/// label placements.
/// </description>
/// </item>
/// </list>
/// <para>
/// A value axis is named for the values it carries, never for the direction its lines run: an X value is a vertical
/// screen line, a Y value a horizontal one (the reference-line convention). A series is drawn from both and is placed again when
/// either changes. A move of the plot reaches both.
/// </para>
/// <para>
/// Internal: the dirty model is an implementation detail, not a public API.
/// </para>
/// </remarks>
[Flags]
internal enum ValueAxes
{
    /// <summary>Neither axis.</summary>
    None = 0,

    /// <summary>The horizontal value axis, X: <c>MinimumX</c> to <c>MaximumX</c>.</summary>
    Horizontal = 1 << 0,

    /// <summary>The vertical value axis, Y: <c>MinimumY</c> to <c>MaximumY</c>.</summary>
    Vertical = 1 << 1,

    /// <summary>Both axes: what a move of the plot, a template or a font reaches.</summary>
    Both = Horizontal | Vertical,
}
