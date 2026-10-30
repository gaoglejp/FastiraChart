using System.Windows;
using System.Windows.Media;

namespace FastiraChart.Rendering;

/// <summary>
/// A drawing layer that is told it is stale and builds what it draws when WPF next renders it.
/// </summary>
/// <typeparam name="TFrame">Everything one render of the layer draws.</typeparam>
/// <remarks>
/// <para>
/// Every layer the control draws uses it — the series, the
/// grid, the reference and origin lines, the tick marks and the labels — so a change to any of them costs the same
/// thing: a flag and an <see cref="UIElement.InvalidateVisual"/>, with the build left to the render.
/// </para>
/// <para>
/// <b>One build per render, however many changes.</b> <see cref="Invalidate"/> only marks the frame stale. The
/// frame is built from the control's current state when the layer is rendered, and then kept until the next
/// change. Two changes to one layer before a frame are one build; changes to three layers are three builds, one
/// each; a layer nothing changed is not built at all.
/// </para>
/// <para>
/// <b>A build is a composition</b>. A build does not place the whole layer again from the current
/// state: the control's builders keep each layer's geometry and its style apart, each valid until a change
/// to something it is drawn from, and a build puts the frame together from whichever of them still stands: a brush
/// change re-strokes the lines already placed, an append places the one series that changed. Which parts are reused
/// is the control's business; this class only decides <em>when</em> a frame is composed.
/// </para>
/// <para>
/// <b>A redraw with nothing new is not a build.</b> WPF can ask for a render without anything having changed — the
/// layer's own <see cref="UIElement.InvalidateVisual"/>, a template arrange — and the frame it already has is drawn
/// again as it is. <see cref="Renders"/> and <see cref="Reuses"/> count it.
/// </para>
/// <para>
/// The element itself is the same one the layer always was: one element, no children, never hit-tested.
/// </para>
/// </remarks>
internal abstract class DeferredLayer<TFrame> : FrameworkElement
{
    private readonly Func<TFrame>? _build;
    private TFrame _frame;
    private bool _stale;
    private bool _holding;
    private bool _renderedWhileHeld;
    private RectangleGeometry? _clip;
    private Rect _clipBounds;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferredLayer{TFrame}"/> class.
    /// </summary>
    /// <param name="build">
    /// Builds the frame from the control as it is when called; null for a layer that is only ever handed frames,
    /// as internal validation's standalone layers are.
    /// </param>
    /// <param name="empty">The frame drawn before the first build: nothing.</param>
    protected DeferredLayer(Func<TFrame>? build, TFrame empty)
    {
        _build = build;
        _frame = empty;

        // A drawing surface only: never hit-tested, nothing of its own to measure.
        IsHitTestVisible = false;
    }

    /// <summary>How many times WPF has rendered the layer.</summary>
    /// <remarks>Read-only instrumentation for internal validation.</remarks>
    internal int Renders { get; private set; }

    /// <summary>How many of those renders drew the frame the layer already had, building nothing.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation: a redraw with no change in between, or a
    /// render held back during the template arrange.
    /// </remarks>
    internal int Reuses { get; private set; }

    /// <summary>Whether the layer is held while the control arranges its template; see <see cref="Hold"/>.</summary>
    /// <remarks>
    /// A render in that window is replaced in the same layout pass (read so such a render makes no
    /// recording; see <see cref="RecordedPart{TPart}"/>).
    /// </remarks>
    protected bool IsHeld => _holding;

    /// <summary>Whether a change has come since the frame was last built.</summary>
    /// <remarks>Read-only instrumentation for internal validation. Reading it builds nothing.</remarks>
    internal bool IsStale => _stale;

    /// <summary>The frame, built now if a change has come since it was last built.</summary>
    /// <remarks>
    /// What the next render draws. Internal validation's instrumentation reads through it, so a read is always what the
    /// current state draws, and the render that follows a read builds nothing more.
    /// </remarks>
    protected TFrame Current
    {
        get
        {
            if (_stale && _build is not null)
            {
                // Cleared first, so a builder that throws does not leave the layer rebuilding on every read.
                _stale = false;
                _frame = _build();
            }

            return _frame;
        }
    }

    /// <summary>
    /// Marks the frame stale and requests a redraw; the frame is built when the redraw comes.
    /// </summary>
    /// <remarks>
    /// Constant time. WPF renders an element once per layout pass however many times it was invalidated, so any
    /// number of calls before a frame cost one build.
    /// </remarks>
    public void Invalidate()
    {
        _stale = true;

        InvalidateVisual();
    }

    /// <summary>
    /// Holds back the build while the control arranges its template, until <see cref="Release"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The control arranges its template — this layer among it — before it knows the plot that arrange produces,
    /// and only then marks the layers that plot moved. A stale frame built inside the template arrange would be
    /// built against the previous plot and at once again against the new one: two builds in one frame. Held, a
    /// render in that window draws the frame it already has and builds nothing; <see cref="Release"/> asks for the
    /// render again, in the same layout pass and before anything is presented, and that render builds once.
    /// </para>
    /// <para>
    /// Holding changes when a frame is built, never what it is built from.
    /// </para>
    /// </remarks>
    public void Hold() => _holding = true;

    /// <summary>
    /// Ends <see cref="Hold"/>; a render that was held back is asked for again, so a stale frame is never what
    /// stays on screen.
    /// </summary>
    public void Release()
    {
        _holding = false;

        if (_renderedWhileHeld)
        {
            _renderedWhileHeld = false;
            InvalidateVisual();
        }
    }

    /// <summary>Replaces the frame outright and requests a redraw.</summary>
    /// <remarks>
    /// The control never calls this; it invalidates. Internal validation hands a layer a frame directly — a
    /// standalone layer, or a deliberately damaged frame its detectors must catch — and this is how.
    /// </remarks>
    protected void Replace(TFrame frame)
    {
        _frame = frame;
        _stale = false;

        InvalidateVisual();
    }

    /// <inheritdoc />
    protected sealed override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        TFrame frame;

        Renders++;

        if (!_stale || _build is null || _holding)
        {
            Reuses++;
        }

        if (_stale && _holding)
        {
            _renderedWhileHeld = true;
            frame = _frame;
        }
        else
        {
            frame = Current;
        }

        Draw(drawingContext, frame);
    }

    /// <summary>Draws one frame. Builds nothing and computes no coordinate.</summary>
    protected abstract void Draw(DrawingContext drawingContext, TFrame frame);

    /// <summary>A frozen clip over the plot rectangle, made again only when the rectangle moves.</summary>
    /// <remarks>
    /// A drawing input kept across renders, so a render of a clipped layer does not allocate a new
    /// rectangle geometry for a rectangle that has not moved. The geometry is the layer's own and frozen; only the
    /// latest rectangle's is kept.
    /// </remarks>
    protected Geometry ClipFor(Rect plot)
    {
        if (_clip is null || _clipBounds != plot)
        {
            var clip = new RectangleGeometry(plot);

            clip.Freeze();
            _clip = clip;
            _clipBounds = plot;
        }

        return _clip;
    }
}
