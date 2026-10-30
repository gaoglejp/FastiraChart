using System.Windows;
using System.Windows.Media;
using FastiraChart.Rendering;

namespace FastiraChart.ReferenceLines;

/// <summary>
/// One reference line ready to draw: where it came from, where it goes, and how it is stroked.
/// </summary>
/// <param name="Index">Its position in <see cref="FastiraChartControl.ReferenceLines"/>.</param>
/// <param name="Line">Its geometry.</param>
/// <param name="Pen">The pen to stroke it with, or null when its style draws nothing.</param>
/// <remarks>
/// The index is kept so internal validation can tie every generated line back to the item that asked
/// for it; two items at the same value are two lines, and the index is what tells them apart.
/// </remarks>
internal readonly record struct PlacedReferenceLine(int Index, PlotLine Line, Pen? Pen);

/// <summary>
/// Everything one frame of reference and origin lines draws.
/// </summary>
/// <param name="References">Reference lines, in collection order.</param>
/// <param name="HorizontalOrigin">The y = 0 line, or null.</param>
/// <param name="VerticalOrigin">The x = 0 line, or null.</param>
/// <param name="OriginPen">The pen for both origin lines, or null to draw neither.</param>
/// <param name="Plot">The plot rectangle the stroke is clipped to.</param>
internal readonly record struct ReferenceFrame(
    IReadOnlyList<PlacedReferenceLine> References,
    PlotLine? HorizontalOrigin,
    PlotLine? VerticalOrigin,
    Pen? OriginPen,
    Rect Plot);

/// <summary>
/// Draws every reference line and both origin lines for the whole control.
/// </summary>
/// <remarks>
/// <para>
/// The drawing stage, and the only part of the reference-line pipeline that touches WPF rendering. It receives finished
/// geometry and pens and strokes them; it computes no coordinate of its own.
/// </para>
/// <para>
/// <b>One element, whatever the line count</b>, like the grid and tick layers. A
/// <see cref="FrameworkElement"/> per reference line would grow the visual tree with the collection;
/// drawing into the <see cref="DrawingContext"/> keeps the child count at zero for ten lines or five
/// hundred.
/// </para>
/// <para>
/// <b>Z-order inside the layer is drawing order</b>: every reference line first, in collection order,
/// then the origin lines. An origin and a reference at the same value can then both exist, and the origin
/// is the one seen — the line that means zero keeps that meaning. The layer as a whole sits above the
/// major grid and below the plot content and the axes; that order is the template's.
/// </para>
/// <para>
/// The stroke is clipped to the plot rectangle, for the reason <see cref="Gridlines.MajorGridLayer"/>
/// gives: the geometry already lies inside the plot, and the clip trims only the half of a pen's width
/// that would spill into an axis band from a line drawn exactly on an edge. Nothing is moved inward to
/// avoid it.
/// </para>
/// <para>
/// <b>Built when drawn</b>. A change to anything the lines are drawn from — the collection, a line
/// in it, the origin style, the ranges, the plot — only marks the layer stale; the lines are placed when WPF next
/// renders it, once however many changes came before. See <see cref="DeferredLayer{TFrame}"/>.
/// </para>
/// </remarks>
internal sealed class ReferenceLineLayer : DeferredLayer<ReferenceFrame>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceLineLayer"/> class, handed its lines by
    /// <see cref="Update"/> rather than building them.
    /// </summary>
    public ReferenceLineLayer()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceLineLayer"/> class.
    /// </summary>
    /// <param name="build">Places the lines from the control as it is when called.</param>
    public ReferenceLineLayer(Func<ReferenceFrame>? build)
        : base(build, new ReferenceFrame([], null, null, null, default))
    {
    }

    /// <summary>The reference lines this layer draws, in drawing order.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, which compares the lines generated here
    /// with the collection and, separately, with the lines drawn, read back from the render data.
    /// Reading it brings the frame up to date first, exactly as the next render would. Internal: it is
    /// not a public geometry API.
    /// </remarks>
    internal IReadOnlyList<PlacedReferenceLine> References => Current.References;

    /// <summary>The horizontal origin line (y = 0), or null when none is drawn.</summary>
    internal PlotLine? HorizontalOrigin => Current.HorizontalOrigin;

    /// <summary>The vertical origin line (x = 0), or null when none is drawn.</summary>
    internal PlotLine? VerticalOrigin => Current.VerticalOrigin;

    /// <summary>The pen the origin lines are stroked with, or null to draw none.</summary>
    internal Pen? OriginPen => Current.OriginPen;

    /// <summary>The plot rectangle the stroke is clipped to.</summary>
    internal Rect PlotClip => Current.Plot;

    /// <summary>
    /// Replaces the geometry outright and requests a redraw. The control never calls this; see
    /// <see cref="DeferredLayer{TFrame}.Replace"/>.
    /// </summary>
    /// <param name="references">Reference lines, in collection order.</param>
    /// <param name="horizontalOrigin">The y = 0 line, or null.</param>
    /// <param name="verticalOrigin">The x = 0 line, or null.</param>
    /// <param name="originPen">The pen for both origin lines, or null to draw neither.</param>
    /// <param name="plot">The plot rectangle the stroke is clipped to.</param>
    public void Update(
        IReadOnlyList<PlacedReferenceLine> references,
        PlotLine? horizontalOrigin,
        PlotLine? verticalOrigin,
        Pen? originPen,
        Rect plot)
        => Replace(new ReferenceFrame(references, horizontalOrigin, verticalOrigin, originPen, plot));

    /// <inheritdoc />
    protected override void Draw(DrawingContext drawingContext, ReferenceFrame frame)
    {
        var drawsOrigin = frame.OriginPen is not null && (frame.HorizontalOrigin is not null || frame.VerticalOrigin is not null);

        if (frame.References.Count == 0 && !drawsOrigin)
        {
            return;
        }

        drawingContext.PushClip(ClipFor(frame.Plot));

        foreach (var reference in frame.References)
        {
            if (reference.Pen is { } pen)
            {
                drawingContext.DrawLine(pen, reference.Line.Start, reference.Line.End);
            }
        }

        // Origins last, so an origin is never painted under a reference line at the same value. Horizontal
        // before vertical, matching the grid's order; the two never overlap except at the one point where
        // they cross, where both are the origin pen.
        if (frame.OriginPen is { } originPen)
        {
            if (frame.HorizontalOrigin is { } horizontal)
            {
                drawingContext.DrawLine(originPen, horizontal.Start, horizontal.End);
            }

            if (frame.VerticalOrigin is { } vertical)
            {
                drawingContext.DrawLine(originPen, vertical.Start, vertical.End);
            }
        }

        drawingContext.Pop();
    }
}
