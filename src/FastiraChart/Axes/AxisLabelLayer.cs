using System.Windows;
using System.Windows.Media;
using FastiraChart.Rendering;

namespace FastiraChart.Axes;

/// <summary>
/// One label, paired with where it goes.
/// </summary>
/// <param name="Text">The measured text, ready to draw.</param>
/// <param name="Placement">The geometry computed for it.</param>
internal readonly record struct PlacedLabel(FormattedText Text, LabelPlacement Placement)
{
    /// <summary>
    /// <see cref="LabelPlacement.Transform"/> as a frozen transform, made once when the label is placed, or null.
    /// </summary>
    /// <remarks>
    /// A drawing input kept with the placement, so redrawing a thousand labels that have not moved does
    /// not allocate a thousand transforms. Drawn only while it still equals the placement's matrix: a label copied
    /// with a different placement — as internal validation's damaged frames are — draws that placement, never this.
    /// </remarks>
    public MatrixTransform? Transform { get; init; }
}

/// <summary>
/// Everything one frame of labels draws.
/// </summary>
/// <param name="Horizontal">The horizontal axis's labels — the X values, on the bottom or top edge.</param>
/// <param name="Vertical">The vertical axis's labels — the Y values, on the left or right edge.</param>
/// <param name="Style">
/// The brush every label's text carries, or null when unknown (a frame internal validation hands the layer). Two frames whose
/// part is the same list in the same style draw that part identically, which is what lets the layer replay it.
/// </param>
/// <remarks>
/// Two parts, kept apart so a change to one axis's range places that axis's labels again and reuses
/// the other's placements and measured text. The horizontal part is drawn first.
/// </remarks>
internal readonly record struct LabelFrame(IReadOnlyList<PlacedLabel> Horizontal, IReadOnlyList<PlacedLabel> Vertical, Brush? Style = null)
{
    /// <summary>Every label, in drawing order: the two parts read as one, made once with the frame.</summary>
    /// <remarks>
    /// The control hands the frame the list it handed the last one when neither part changed, so the labels of a
    /// frame composed again for its brush alone are the very list they were.
    /// </remarks>
    public IReadOnlyList<PlacedLabel> Labels { get; init; } = JoinedList<PlacedLabel>.Of(Horizontal, Vertical, null);
}

/// <summary>
/// Draws tick labels for the whole control.
/// </summary>
/// <remarks>
/// <para>
/// The drawing stage, and the only part of labelling that touches WPF rendering. It receives
/// finished text and finished geometry and does nothing but place one under the other.
/// </para>
/// <para>
/// One element draws every label, the same way <see cref="AxisTickLayer"/> draws every tick. A
/// <see cref="FrameworkElement"/> per label — a <c>TextBlock</c>, say — would attach a dependency
/// object, a layout slot, a measure pass and an arrange pass to each one, and the visual tree
/// would grow with the label count. That is the cost that decides whether an axis stays cheap at
/// high label densities, and it is avoided here by construction rather than optimised
/// away later. The child count is zero whether there are three labels or three hundred.
/// </para>
/// <para>
/// <b>Built when drawn</b>. A change to anything the labels are drawn from — the text, the plot,
/// the axis bands, an axis's position, visibility, rotation, anchor or gap — only marks the layer stale; the
/// labels are placed when WPF next renders it, once however many changes came before. See
/// <see cref="DeferredLayer{TFrame}"/>.
/// </para>
/// <para>
/// <b>An unchanged part is replayed</b>. Each axis's labels are one part of the frame. A part the control
/// hands the layer unchanged — the same list, in the same brush — is recorded once and from then on replayed, so a
/// render of the layer lays out only the labels that changed: a horizontal range re-draws the bottom labels and replays
/// the left ones, and a change inside a mutable <see cref="System.Windows.Controls.Control.Foreground"/> replays both, the
/// recording drawing the live brush. See <see cref="RecordedPart{TPart}"/>.
/// </para>
/// <para>
/// <b>Placed once, redrawn as placed</b>. The control keeps the measured text, the placements and the
/// brush apart: a new <see cref="System.Windows.Controls.Control.Foreground"/> re-colours the measured text in place
/// and reuses every placement, a rotation re-places the measured text without measuring it again. This layer is
/// handed the result and draws it.
/// </para>
/// </remarks>
internal sealed class AxisLabelLayer : DeferredLayer<LabelFrame>
{
    private readonly RecordedPart<IReadOnlyList<PlacedLabel>> _horizontal = new();
    private readonly RecordedPart<IReadOnlyList<PlacedLabel>> _vertical = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AxisLabelLayer"/> class, handed its labels by
    /// <see cref="Update"/> rather than building them.
    /// </summary>
    public AxisLabelLayer()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AxisLabelLayer"/> class.
    /// </summary>
    /// <param name="build">Places the labels from the control as it is when called.</param>
    public AxisLabelLayer(Func<LabelFrame>? build)
        : base(build, new LabelFrame([], []))
    {
        // The one place a label is allowed to be cut off. A label whose axis band is too narrow
        // still draws in full — it is never shortened, thinned or dropped — but it must not paint
        // outside the control and over whatever sits next to it. The layer fills the template's
        // axis host, which spans the control, so clipping to its own bounds is exactly the
        // control's visual bounds and nothing tighter. In particular this is not the plot clip:
        // labels live in the axis band, outside the plot, and ClipToPlotArea must never reach
        // them.
        ClipToBounds = true;
    }

    /// <summary>
    /// The labels this layer draws, in drawing order.
    /// </summary>
    /// <remarks>
    /// Read-only internal instrumentation, so that the labels
    /// <em>generated</em> can be counted separately from the labels <em>drawn</em>, which internal
    /// validation reads back from the render data. Reading it brings the frame up to date first, exactly
    /// as the next render would. Internal: it is not a public label collection.
    /// </remarks>
    internal IReadOnlyList<PlacedLabel> Labels => Current.Labels;

    /// <summary>The horizontal axis's labels this layer draws: the X part of the frame.</summary>
    /// <remarks>Read-only instrumentation for internal validation, read as <see cref="Labels"/> is.</remarks>
    internal IReadOnlyList<PlacedLabel> HorizontalLabels => Current.Horizontal;

    /// <summary>The vertical axis's labels this layer draws: the Y part of the frame.</summary>
    /// <remarks>Read-only instrumentation for internal validation, read as <see cref="Labels"/> is.</remarks>
    internal IReadOnlyList<PlacedLabel> VerticalLabels => Current.Vertical;

    /// <summary>How often each axis's labels were recorded, and how often a render replayed the recording.</summary>
    /// <remarks>Read-only instrumentation for internal validation.</remarks>
    internal (int Recordings, int Replays, int Held) Recorded
        => (_horizontal.Recordings + _vertical.Recordings, _horizontal.Replays + _vertical.Replays, (_horizontal.IsRecorded ? 1 : 0) + (_vertical.IsRecorded ? 1 : 0));

    /// <summary>Whether the last render drew each axis's part through its recording rather than directly.</summary>
    /// <remarks>Read-only instrumentation for internal validation.</remarks>
    internal (bool Horizontal, bool Vertical) LastReplayed => (_horizontal.Replayed, _vertical.Replayed);

    /// <summary>The horizontal axis's recording, or null.</summary>
    /// <remarks>Read-only instrumentation for internal validation, which holds it only weakly.</remarks>
    internal Drawing? HorizontalRecording => _horizontal.Recording;

    /// <summary>
    /// Replaces the labels outright and requests a redraw. The control never calls this; see
    /// <see cref="DeferredLayer{TFrame}.Replace"/>.
    /// </summary>
    /// <param name="labels">The labels to draw, in this element's coordinate space.</param>
    public void Update(IReadOnlyList<PlacedLabel> labels) => Replace(new LabelFrame(labels, []));

    /// <inheritdoc />
    protected override void Draw(DrawingContext drawingContext, LabelFrame frame)
    {
        var frozen = frame.Style?.IsFrozen == true;

        if (frame.Horizontal.Count > 0)
        {
            _horizontal.Draw(drawingContext, frame.Horizontal, frame.Style, frozen, !IsHeld, DrawLabels);
        }

        if (frame.Vertical.Count > 0)
        {
            _vertical.Draw(drawingContext, frame.Vertical, frame.Style, frozen, !IsHeld, DrawLabels);
        }
    }

    /// <summary>Draws one part's labels, each at its own transform.</summary>
    private static void DrawLabels(DrawingContext drawingContext, IReadOnlyList<PlacedLabel> labels)
    {
        foreach (var label in labels)
        {
            // The transform carries the anchor, the rotation and the outward clearance already,
            // so the text is drawn at its own origin and every coordinate decision stays in the
            // geometry stage where it can be checked without a window.
            var transform = label.Transform is { } frozen && frozen.Value == label.Placement.Transform
                ? frozen
                : new MatrixTransform(label.Placement.Transform);

            drawingContext.PushTransform(transform);
            drawingContext.DrawText(label.Text, new Point(0, 0));
            drawingContext.Pop();
        }
    }
}
