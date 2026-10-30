using System.Windows;
using System.Windows.Media;
using FastiraChart.Rendering;

namespace FastiraChart.Gridlines;

/// <summary>
/// Everything one frame of the grid draws.
/// </summary>
/// <param name="Horizontal">Horizontal grid lines, in this element's coordinate space.</param>
/// <param name="Vertical">Vertical grid lines, in this element's coordinate space.</param>
/// <param name="Plot">The plot rectangle the stroke is clipped to.</param>
/// <param name="Pen">The pen to stroke with, or null to draw nothing.</param>
internal readonly record struct GridFrame(IReadOnlyList<GridLine> Horizontal, IReadOnlyList<GridLine> Vertical, Rect Plot, Pen? Pen);

/// <summary>
/// Draws the major grid for the whole control.
/// </summary>
/// <remarks>
/// <para>
/// The drawing stage, and the only part of the grid that touches WPF rendering. It receives
/// finished geometry and strokes it; it computes no coordinate of its own.
/// </para>
/// <para>
/// One element draws every grid line, the way <see cref="Axes.AxisTickLayer"/> draws every tick.
/// A <see cref="FrameworkElement"/> or a <c>Line</c> per grid line would grow the visual tree with
/// the tick count, which is exactly what the axis layers were built to avoid; drawing into the
/// <see cref="DrawingContext"/> keeps the child count at zero whether there are two lines or two
/// thousand.
/// </para>
/// <para>
/// The stroke is clipped to the plot rectangle. The geometry already lies inside it, end to end, so
/// the clip trims only the half of a stroke's width that would otherwise spill over the plot edge
/// from a line drawn exactly on it — at a normalized position of 0 or 1. Without it an edge line
/// would paint half a pen into the axis band. Clipping does not remove the line, and the line is
/// not moved inward to avoid it: an edge tick has an edge grid line.
/// </para>
/// <para>
/// <b>Built when drawn</b>. A change to anything the grid is drawn from — its visibility, brush or
/// thickness, the ticks, the plot — only marks the layer stale; the lines are placed when WPF next renders it,
/// once however many changes came before. See <see cref="DeferredLayer{TFrame}"/>.
/// </para>
/// </remarks>
internal sealed class MajorGridLayer : DeferredLayer<GridFrame>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MajorGridLayer"/> class, handed its lines by
    /// <see cref="Update"/> rather than building them.
    /// </summary>
    public MajorGridLayer()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MajorGridLayer"/> class.
    /// </summary>
    /// <param name="build">Builds the grid from the control as it is when called.</param>
    public MajorGridLayer(Func<GridFrame>? build)
        : base(build, new GridFrame([], [], default, null))
    {
    }

    /// <summary>The horizontal lines this layer draws, in drawing order.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, so the lines <em>generated</em> can be
    /// compared with the tick sequence and, separately, with the lines <em>drawn</em>, which internal
    /// validation reads back from the render data. Reading it brings the frame up to date first, exactly as the
    /// next render would. Internal: it is not a public grid collection.
    /// </remarks>
    internal IReadOnlyList<GridLine> HorizontalLines => Current.Horizontal;

    /// <summary>The vertical lines this layer draws, in drawing order.</summary>
    internal IReadOnlyList<GridLine> VerticalLines => Current.Vertical;

    /// <summary>The plot rectangle the stroke is clipped to.</summary>
    internal Rect PlotClip => Current.Plot;

    /// <summary>Whether the layer has a pen to stroke with; false draws nothing.</summary>
    internal bool HasPen => Current.Pen is not null;

    /// <summary>The pen the grid is stroked with, or null.</summary>
    /// <remarks>
    /// Read by the lens, which strokes its own grid lines with this very pen, so the two grids are one style
    /// and the lens makes no pen of its own. Reading it brings the frame up to date first, as the next render would.
    /// </remarks>
    internal Pen? Pen => Current.Pen;

    /// <summary>
    /// Replaces the geometry outright and requests a redraw. The control never calls this; see
    /// <see cref="DeferredLayer{TFrame}.Replace"/>.
    /// </summary>
    /// <param name="horizontal">Horizontal grid lines, in this element's coordinate space.</param>
    /// <param name="vertical">Vertical grid lines, in this element's coordinate space.</param>
    /// <param name="plot">The plot rectangle the stroke is clipped to.</param>
    /// <param name="pen">The pen to stroke with, or null to draw nothing.</param>
    public void Update(IReadOnlyList<GridLine> horizontal, IReadOnlyList<GridLine> vertical, Rect plot, Pen? pen)
        => Replace(new GridFrame(horizontal, vertical, plot, pen));

    /// <inheritdoc />
    protected override void Draw(DrawingContext drawingContext, GridFrame frame)
    {
        if (frame.Pen is not { } pen || (frame.Horizontal.Count == 0 && frame.Vertical.Count == 0))
        {
            return;
        }

        drawingContext.PushClip(ClipFor(frame.Plot));

        // Horizontal before vertical, which fixes the order the render data records them in. It has
        // no visual meaning: both are one brush at one thickness, and neither is drawn over the other
        // for emphasis.
        foreach (var line in frame.Horizontal)
        {
            drawingContext.DrawLine(pen, line.Start, line.End);
        }

        foreach (var line in frame.Vertical)
        {
            drawingContext.DrawLine(pen, line.Start, line.End);
        }

        drawingContext.Pop();
    }
}
