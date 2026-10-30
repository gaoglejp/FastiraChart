using System.Windows.Media;
using FastiraChart.Rendering;

namespace FastiraChart.Axes;

/// <summary>
/// Everything one frame of tick marks draws.
/// </summary>
/// <param name="Horizontal">
/// The horizontal axis's marks — drawn from the X values, on the bottom or top edge — in this element's coordinate
/// space.
/// </param>
/// <param name="Vertical">The vertical axis's marks — drawn from the Y values, on the left or right edge.</param>
/// <param name="Pen">The pen to stroke them with, or null to draw nothing.</param>
/// <remarks>
/// Two parts, kept apart so a change to one axis's range places that axis's marks again and reuses
/// the other's. The horizontal part is drawn first.
/// </remarks>
internal readonly record struct TickFrame(IReadOnlyList<TickMark> Horizontal, IReadOnlyList<TickMark> Vertical, Pen? Pen)
{
    /// <summary>A frame of one list of marks, drawn as given: how internal validation hands a layer a frame.</summary>
    public TickFrame(IReadOnlyList<TickMark> marks, Pen? pen)
        : this(marks, [], pen)
    {
    }

    /// <summary>Every mark, in drawing order: the two parts read as one, made once with the frame.</summary>
    /// <remarks>
    /// The control hands the frame the list it handed the last one when neither part changed, so the marks of a
    /// frame composed again for its pen alone are the very list they were.
    /// </remarks>
    public IReadOnlyList<TickMark> Marks { get; init; } = JoinedList<TickMark>.Of(Horizontal, Vertical, null);
}

/// <summary>
/// Draws tick marks for the whole control.
/// </summary>
/// <remarks>
/// <para>
/// The third of the three stages, and the only one that touches WPF. It receives finished
/// geometry and does nothing but stroke it.
/// </para>
/// <para>
/// One element draws every tick. A <see cref="System.Windows.FrameworkElement"/> per tick would put a
/// dependency object, a layout slot and a render pass behind each mark, which is the cost
/// that matters once tick counts grow — and it is avoided here by construction rather than
/// optimised away later. Drawing directly into the <see cref="DrawingContext"/> keeps the
/// visual tree the same size whether there are three ticks or three hundred.
/// </para>
/// <para>
/// <b>Built when drawn</b>. A change to anything the marks are drawn from — the ticks, the
/// plot, the axis bands, an axis's position or visibility, the brush — only marks the layer stale; the
/// marks are placed when WPF next renders it, once however many changes came before. See
/// <see cref="DeferredLayer{TFrame}"/>.
/// </para>
/// <para>
/// <b>An unchanged part is replayed</b>. Each axis's marks are one part of the frame; a part handed to the
/// layer unchanged — the same list, stroked with the same brush at the same thickness — is recorded once and from then
/// on replayed (<see cref="RecordedPart{TPart}"/>). A pen made again over the same live brush draws exactly what the
/// recording draws, so it replays too.
/// </para>
/// </remarks>
internal sealed class AxisTickLayer : DeferredLayer<TickFrame>
{
    private readonly RecordedPart<IReadOnlyList<TickMark>> _horizontal = new();
    private readonly RecordedPart<IReadOnlyList<TickMark>> _vertical = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AxisTickLayer"/> class, handed its marks by
    /// <see cref="Update"/> rather than building them.
    /// </summary>
    public AxisTickLayer()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AxisTickLayer"/> class.
    /// </summary>
    /// <param name="build">Places the marks from the control as it is when called.</param>
    public AxisTickLayer(Func<TickFrame>? build)
        : base(build, new TickFrame([], null))
    {
    }

    /// <summary>The marks this layer draws, in drawing order.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation. Reading it brings the frame up to date first,
    /// exactly as the next render would. Internal: it is not a public tick collection.
    /// </remarks>
    internal IReadOnlyList<TickMark> Marks => Current.Marks;

    /// <summary>The horizontal axis's marks this layer draws: the X part of the frame.</summary>
    /// <remarks>Read-only instrumentation for internal validation, read as <see cref="Marks"/> is.</remarks>
    internal IReadOnlyList<TickMark> HorizontalMarks => Current.Horizontal;

    /// <summary>The vertical axis's marks this layer draws: the Y part of the frame.</summary>
    /// <remarks>Read-only instrumentation for internal validation, read as <see cref="Marks"/> is.</remarks>
    internal IReadOnlyList<TickMark> VerticalMarks => Current.Vertical;

    /// <summary>The pen the marks are stroked with, or null.</summary>
    /// <remarks>
    /// Read by the lens, which strokes its own X tick marks with this very pen — live over a caller's mutable
    /// <see cref="System.Windows.Controls.Control.Foreground"/>, as the main marks are. Reading it brings the frame up to
    /// date first, as the next render would.
    /// </remarks>
    internal Pen? Pen => Current.Pen;

    /// <summary>How often each axis's marks were recorded, and how often a render replayed the recording.</summary>
    /// <remarks>Read-only instrumentation for internal validation.</remarks>
    internal (int Recordings, int Replays, int Held) Recorded
        => (_horizontal.Recordings + _vertical.Recordings, _horizontal.Replays + _vertical.Replays, (_horizontal.IsRecorded ? 1 : 0) + (_vertical.IsRecorded ? 1 : 0));

    /// <summary>Whether the last render drew each axis's part through its recording rather than directly.</summary>
    /// <remarks>Read-only instrumentation for internal validation.</remarks>
    internal (bool Horizontal, bool Vertical) LastReplayed => (_horizontal.Replayed, _vertical.Replayed);

    /// <summary>
    /// Replaces the geometry outright and requests a redraw. The control never calls this; see
    /// <see cref="DeferredLayer{TFrame}.Replace"/>.
    /// </summary>
    /// <param name="marks">The marks to draw, in this element's coordinate space.</param>
    /// <param name="pen">The pen to stroke them with, or null to draw nothing.</param>
    public void Update(IReadOnlyList<TickMark> marks, Pen? pen) => Replace(new TickFrame(marks, pen));

    /// <inheritdoc />
    protected override void Draw(DrawingContext drawingContext, TickFrame frame)
    {
        if (frame.Pen is not { } pen || frame.Marks.Count == 0)
        {
            return;
        }

        // The style is what the marks look like, not the pen object: a pen made again over the same brush at the same
        // thickness strokes them identically, and over a mutable brush both draw it live.
        var style = (pen.Brush, pen.Thickness);

        if (frame.Horizontal.Count > 0)
        {
            _horizontal.Draw(drawingContext, frame.Horizontal, style, pen.IsFrozen, !IsHeld, (dc, marks) => DrawMarks(dc, marks, pen));
        }

        if (frame.Vertical.Count > 0)
        {
            _vertical.Draw(drawingContext, frame.Vertical, style, pen.IsFrozen, !IsHeld, (dc, marks) => DrawMarks(dc, marks, pen));
        }
    }

    /// <summary>Strokes one part's marks.</summary>
    private static void DrawMarks(DrawingContext drawingContext, IReadOnlyList<TickMark> marks, Pen pen)
    {
        foreach (var mark in marks)
        {
            drawingContext.DrawLine(pen, mark.Start, mark.End);
        }
    }
}
