using System.Windows;
using System.Windows.Media;
using FastiraChart.Rendering;
using FastiraChart.Series;

namespace FastiraChart.Interaction;

/// <summary>
/// Everything one render of the interaction overlay draws: the crosshair and the tooltip.
/// </summary>
/// <param name="HasCrosshair">Whether the crosshair is drawn.</param>
/// <param name="HasTooltip">Whether the tooltip is drawn.</param>
/// <param name="Region">The view the pointer is in: <see cref="PointerRegion.Plot"/> or <see cref="PointerRegion.Lens"/>.</param>
/// <param name="Bounds">That view's rectangle — the plot, or the effective lens — which the crosshair spans and is clipped to.</param>
/// <param name="Pointer">The pointer, in the control's space: the crosshair stands exactly here.</param>
/// <param name="Value">
/// The values the tooltip states, as doubles: in the plot the main X and Y, in the lens the lens X and the Y — read from
/// the pointer inverse, never mapped here.
/// </param>
/// <param name="Pen">The pen the crosshair and the tooltip's border are stroked with.</param>
/// <param name="TooltipText">The tooltip's text, as formatted.</param>
/// <param name="Text">The tooltip's text, measured; null when there is no tooltip.</param>
/// <param name="TooltipBounds">The tooltip's box, in the control's space.</param>
/// <param name="TextOrigin">Where the text is drawn, inside the box.</param>
/// <param name="Fill">The tooltip's background.</param>
internal readonly record struct InteractionFrame(
    bool HasCrosshair,
    bool HasTooltip,
    PointerRegion Region,
    Rect Bounds,
    Point Pointer,
    ChartPoint Value,
    Pen? Pen,
    string TooltipText,
    FormattedText? Text,
    Rect TooltipBounds,
    Point TextOrigin,
    Brush? Fill)
{
    /// <summary>Nothing: no pointer over values, or neither the crosshair nor the tooltip on.</summary>
    public static readonly InteractionFrame Empty = new(false, false, PointerRegion.Outside, Rect.Empty, default, default, null, string.Empty, null, Rect.Empty, default, null);

    /// <summary>Whether anything is drawn.</summary>
    public bool IsEmpty => !HasCrosshair && !HasTooltip;
}

/// <summary>
/// The interaction overlay: one drawing element for the crosshair and the tooltip, whatever the pointer does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Drawn, never placed.</b> Every coordinate comes in the frame, composed by the control from the pointer state:
/// the crosshair at the pointer's own coordinate, its view's rectangle from the very placement the plot and the lens are
/// drawn by. This class computes no coordinate and maps no value.
/// </para>
/// <para>
/// <b>Never in the way.</b> Like every layer of the control it is not hit-tested (<see cref="DeferredLayer{TFrame}"/>), and
/// it sits in a host that is not either, so the overlay never takes a press, a wheel or a move from the chart beneath it.
/// </para>
/// </remarks>
internal sealed class InteractionLayer : DeferredLayer<InteractionFrame>
{
    /// <summary>How opaque the crosshair is drawn: a guide over the data, not data.</summary>
    internal const double CrosshairOpacity = 0.7;

    /// <summary>Initializes a new instance of the <see cref="InteractionLayer"/> class.</summary>
    /// <param name="build">Composes the frame from the control as it is; null for a layer that is only handed frames.</param>
    public InteractionLayer(Func<InteractionFrame>? build)
        : base(build, InteractionFrame.Empty)
    {
    }

    /// <summary>The frame the next render draws, composed now if it is stale.</summary>
    /// <remarks>Read-only instrumentation for internal validation.</remarks>
    internal InteractionFrame Frame => Current;

    /// <summary>Hands the layer a frame directly.</summary>
    /// <remarks>Validation only: the detector cases hand the layer a damaged frame the monitors must catch.</remarks>
    internal void Show(InteractionFrame frame) => Replace(frame);

    /// <inheritdoc />
    protected override void Draw(DrawingContext drawingContext, InteractionFrame frame)
    {
        if (frame.HasCrosshair && frame.Pen is { } pen)
        {
            var b = frame.Bounds;
            var p = frame.Pointer;

            // Clipped to the view the pointer is in, so neither line, nor its stroke's half-width at an edge, reaches an
            // axis band or past the lens.
            drawingContext.PushClip(ClipFor(b));
            drawingContext.PushOpacity(CrosshairOpacity);
            drawingContext.DrawLine(pen, new Point(p.X, b.Top), new Point(p.X, b.Bottom));
            drawingContext.DrawLine(pen, new Point(b.Left, p.Y), new Point(b.Right, p.Y));
            drawingContext.Pop();
            drawingContext.Pop();
        }

        if (frame.HasTooltip && frame.Text is { } text)
        {
            var box = frame.TooltipBounds;
            var half = frame.Pen is { } border ? border.Thickness / 2 : 0;

            // Inset by half the stroke, so the border lies inside the box that was placed.
            drawingContext.DrawRectangle(frame.Fill, frame.Pen, new Rect(box.X + half, box.Y + half, Math.Max(0, box.Width - (2 * half)), Math.Max(0, box.Height - (2 * half))));
            drawingContext.DrawText(text, frame.TextOrigin);
        }
    }
}
