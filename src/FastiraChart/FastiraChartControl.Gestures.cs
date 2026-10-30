using System.Windows;
using System.Windows.Input;
using FastiraChart.Interaction;
using FastiraChart.Lens;

namespace FastiraChart;

/// <summary>What a left-button drag over the chart is doing.</summary>
internal enum InteractionDrag
{
    /// <summary>No drag.</summary>
    None,

    /// <summary>The main ranges follow the pointer: both axes, span kept.</summary>
    MainPan,

    /// <summary>The lens and its source move across together: horizontal only, source span kept.</summary>
    LensDrag,
}

// <summary>
// Zoom, pan and lens drag: the wheel and the left button turned into new ranges and a new lens state.
// </summary>
// <remarks>
// <para>
// <b>One coordinate system.</b> Every gesture reads the pointer through the pointer inverse (<see cref="ResolvePointer"/>):
// the region it is in, the main value under it and the lens value under it. Nothing here maps a coordinate of its own.
// </para>
// <para>
// <b>State in, state out.</b> A gesture ends in the range and lens properties, written with
// <see cref="DependencyObject.SetCurrentValue"/> so a caller's binding survives; the dirty model then draws the
// change exactly as it draws one the caller made — coalesced into one build per frame, the lens alone for a lens
// gesture. No geometry is moved, no layer is transformed, and no layout layer is added.
// </para>
// <para>
// <b>Off unless asked for.</b> Each gesture family has a switch, off by default, so no existing chart starts taking the
// wheel or the left button from its host. A gesture that is off, or outside the plot, leaves the event unhandled for the
// parent. The switches are public (<see cref="IsZoomEnabled"/>, <see cref="IsPanEnabled"/>,
// <see cref="IsLensInteractionEnabled"/>), each off by default.
// </para>
// <para>
// <b>Mouse capture only while dragging.</b> Taken at the press, released at the release, and a drag ends whenever capture
// is lost for any reason — another element, the window deactivated, the control disabled or removed.
// </para>
// </remarks>
public partial class FastiraChartControl
{
    /// <summary>Identifies the <see cref="IsZoomEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsZoomEnabledProperty =
        DependencyProperty.Register(
            nameof(IsZoomEnabled),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(false, OnGestureSwitchChanged));

    /// <summary>Identifies the <see cref="IsPanEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsPanEnabledProperty =
        DependencyProperty.Register(
            nameof(IsPanEnabled),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(false, OnGestureSwitchChanged));

    /// <summary>Identifies the <see cref="IsLensInteractionEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsLensInteractionEnabledProperty =
        DependencyProperty.Register(
            nameof(IsLensInteractionEnabled),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(false, OnGestureSwitchChanged));

    // The switches as last set, read on every wheel step and press without a property lookup.
    private bool _zoomEnabled;
    private bool _panEnabled;
    private bool _lensInteractionEnabled;
    private InteractionDrag _drag;
    private DragStart _dragStart;

    /// <summary>What the chart was when a drag began: every drag position is computed from it, never from the last one.</summary>
    /// <param name="Pointer">The press position.</param>
    /// <param name="Plot">The plot every layer was placed against.</param>
    /// <param name="MinimumX">The main horizontal minimum.</param>
    /// <param name="MaximumX">The main horizontal maximum.</param>
    /// <param name="MinimumY">The main vertical minimum.</param>
    /// <param name="MaximumY">The main vertical maximum.</param>
    /// <param name="LensMinimum">The lens source as requested.</param>
    /// <param name="LensMaximum">The lens source maximum as requested.</param>
    /// <param name="LensBasisMinimum">The source a lens drag moves: the request when it lies in the main range, else its effective part.</param>
    /// <param name="LensBasisMaximum">The basis maximum.</param>
    /// <param name="LensPosition">The lens position.</param>
    /// <param name="LensTravel">How far the lens can travel across the plot, in DIP: the plot width less the lens width.</param>
    private readonly record struct DragStart(
        Point Pointer,
        Rect Plot,
        double MinimumX,
        double MaximumX,
        double MinimumY,
        double MaximumY,
        double LensMinimum,
        double LensMaximum,
        double LensBasisMinimum,
        double LensBasisMaximum,
        double LensPosition,
        double LensTravel);

    /// <summary>
    /// Gets or sets whether the wheel zooms the chart. The default is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On, the wheel over the plot zooms the X range about the value under the pointer, 1.2 times a notch — away from the
    /// user zooms in — and with Shift held the Y range, likewise. Over the lens, with <see cref="IsLensInteractionEnabled"/>
    /// on, it zooms the range the lens shows instead, about the lens value under the pointer, leaving the chart's X range,
    /// the lens's place and its width as they are. A zoom in stops where the range would no longer resolve the plot's
    /// width; a zoom out never overflows.
    /// </para>
    /// <para>
    /// Off, outside the plot, or with Ctrl or Alt held, the wheel is left unhandled for the parent — a
    /// <see cref="System.Windows.Controls.ScrollViewer"/> around the chart still scrolls. A zoom writes the range and lens
    /// source properties as current values, so a binding on them survives it. Like every property of the control it is set
    /// on the control's thread, and may be bound, styled or animated.
    /// </para>
    /// </remarks>
    public bool IsZoomEnabled
    {
        get => (bool)GetValue(IsZoomEnabledProperty);
        set => SetValue(IsZoomEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether a left-button drag over the plot pans the chart. The default is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On, a left drag over the plot moves both ranges with the pointer — the value under the press stays under the
    /// pointer, the spans are kept — with the mouse captured for the drag alone and released with the button. Over the
    /// lens, with <see cref="IsLensInteractionEnabled"/> on, the lens is dragged instead. Turning it off ends a pan in
    /// progress where it stands.
    /// </para>
    /// <para>
    /// Off, or outside the plot, the press is left unhandled for the parent. A pan writes the range properties as current
    /// values, so a binding on them survives it. Like every property of the control it is set on the control's thread,
    /// and may be bound, styled or animated.
    /// </para>
    /// </remarks>
    public bool IsPanEnabled
    {
        get => (bool)GetValue(IsPanEnabledProperty);
        set => SetValue(IsPanEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the lens takes gestures of its own. The default is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On, a left drag inside the lens moves the lens and the range it shows across together — the span kept, the range
    /// held inside the chart's X range by shifting, never by cutting, and the chart's own ranges untouched — and, with
    /// <see cref="IsZoomEnabled"/> on, the wheel inside the lens zooms the range it shows. The lens wins over the plot
    /// beneath it. Turning it off ends a lens drag in progress where it stands.
    /// </para>
    /// <para>
    /// Off, the lens is plain plot to every gesture. A lens drag writes <see cref="LensHorizontalPosition"/>,
    /// <see cref="LensMinimumX"/> and <see cref="LensMaximumX"/> as current values, so a binding on them survives it. Like
    /// every property of the control it is set on the control's thread, and may be bound, styled or animated.
    /// </para>
    /// </remarks>
    public bool IsLensInteractionEnabled
    {
        get => (bool)GetValue(IsLensInteractionEnabledProperty);
        set => SetValue(IsLensInteractionEnabledProperty, value);
    }

    /// <summary>Keeps the switch fields, and ends a drag whose own switch was turned off.</summary>
    /// <remarks>A switch changes nothing drawn, so nothing is marked; pan off leaves a lens drag running, and the reverse.</remarks>
    private static void OnGestureSwitchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;
        var on = (bool)e.NewValue;

        if (e.Property == IsZoomEnabledProperty)
        {
            control._zoomEnabled = on;
        }
        else if (e.Property == IsPanEnabledProperty)
        {
            control._panEnabled = on;

            if (!on && control._drag == InteractionDrag.MainPan)
            {
                control.EndDrag();
            }
        }
        else
        {
            control._lensInteractionEnabled = on;

            if (!on && control._drag == InteractionDrag.LensDrag)
            {
                control.EndDrag();
            }
        }
    }

    /// <summary>What a drag is doing now; <see cref="InteractionDrag.None"/> between drags.</summary>
    /// <remarks>Separate from the pointer state: that says where the pointer is, this what it is doing.</remarks>
    internal InteractionDrag DragState => _drag;

    /// <summary>How many gestures changed state: wheel steps and drag positions that wrote a range or the lens.</summary>
    /// <remarks>
    /// Read-only internal instrumentation. The sum of the per-kind counts in
    /// <see cref="ProcessingCounters"/>, counted where this count was counted, so it means what it meant. Internal: it is
    /// not a public API.
    /// </remarks>
    internal int GestureUpdates => _processingCounters.Gestures;

    /// <summary>Applies one wheel step at a coordinate, as <see cref="OnMouseWheel"/> does.</summary>
    /// <param name="at">The pointer, in the control's space.</param>
    /// <param name="delta">The wheel delta: 120 a notch, positive away from the user (zoom in).</param>
    /// <param name="modifiers">The modifier keys held: none zooms X, Shift alone zooms Y; any other is not a gesture.</param>
    /// <returns>
    /// Whether the step was a gesture of the chart's — zoom on, the pointer inside the plot over values — and so should be
    /// marked handled. A step at a zoom limit is still the chart's, and changes nothing.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Inside the lens, with the lens's gestures on, the wheel zooms the lens source about the lens value under the
    /// pointer; the main X range, the lens's position and its width are not touched. Elsewhere in the plot it zooms the main
    /// X range about the main value under the pointer, and the lens request is left as it is. Shift zooms the main Y range
    /// about the Y value under the pointer, in the lens too: the lens's Y is the main Y.
    /// </para>
    /// </remarks>
    internal bool ApplyWheel(Point at, double delta, ModifierKeys modifiers)
    {
        if (!_zoomEnabled || !double.IsFinite(delta) || delta == 0 || (modifiers != ModifierKeys.None && modifiers != ModifierKeys.Shift))
        {
            return false;
        }

        var s = ResolvePointer(at);

        if (!s.IsInsidePlot || !s.HasData)
        {
            return false;
        }

        var factor = RangeInteraction.WheelFactor(delta);

        if (modifiers == ModifierKeys.Shift)
        {
            if (RangeInteraction.TryZoom(MinimumY, MaximumY, s.Data.Y, factor, _tickPlotBounds.Height, out var low, out var high))
            {
                WriteRange(MinimumYProperty, MaximumYProperty, MaximumY, low, high);
                _processingCounters.Zooms++;
            }

            return true;
        }

        if (s.IsInsideLens && _lensInteractionEnabled)
        {
            if (s.HasLensData)
            {
                ZoomLens(s.LensData.X, factor);
            }

            return true;
        }

        if (RangeInteraction.TryZoom(MinimumX, MaximumX, s.Data.X, factor, _tickPlotBounds.Width, out var left, out var right))
        {
            WriteRange(MinimumXProperty, MaximumXProperty, MaximumX, left, right);
            _processingCounters.Zooms++;
        }

        return true;
    }

    /// <summary>Zooms the lens source about a lens value, the result held inside the main range.</summary>
    /// <remarks>
    /// The effective source is zoomed — what the lens shows, so the value under the pointer stays under it — and the result
    /// is kept inside the main range: a zoom in always is, a zoom out that would leave it is shifted back in, and one as
    /// wide as the main range becomes the main range. Only the source is written.
    /// </remarks>
    private void ZoomLens(double anchor, double factor)
    {
        if (!LensMapping.TryPlace(LensState, _tickPlotBounds, MinimumX, MaximumX, out var placement)
            || !RangeInteraction.TryZoom(placement.SourceMinimum, placement.SourceMaximum, anchor, factor, placement.Bounds.Width, out var low, out var high)
            || !RangeInteraction.TryContain(low, high, MinimumX, MaximumX, out low, out high)
            || (low == LensMinimumX && high == LensMaximumX))
        {
            return;
        }

        WriteRange(LensMinimumXProperty, LensMaximumXProperty, LensMaximumX, low, high);
        _processingCounters.Zooms++;
    }

    /// <summary>Begins a drag at a coordinate, if one starts there: takes the snapshot, takes no capture.</summary>
    /// <returns>Whether a drag began; see <see cref="DragState"/> for which.</returns>
    /// <remarks>
    /// Inside the lens, with the lens's gestures on, a lens drag — the lens wins over the plot beneath it. Elsewhere in the
    /// plot, with pan on, a main pan. Outside the plot, over no values, or already dragging: nothing.
    /// <see cref="StartDrag"/> adds the capture; internal validation calls this to drive drags without a window.
    /// </remarks>
    internal bool TryBeginDrag(Point at)
    {
        if (_drag != InteractionDrag.None)
        {
            return false;
        }

        var s = ResolvePointer(at);
        var plot = _tickPlotBounds;

        if (!s.IsInsidePlot || !s.HasData)
        {
            return false;
        }

        if (s.IsInsideLens && _lensInteractionEnabled)
        {
            if (!LensMapping.TryPlace(LensState, plot, MinimumX, MaximumX, out var placement))
            {
                return false;
            }

            // A request inside the main range is moved as it is; one reaching outside is moved as the part the lens shows,
            // so what the drag asks for lies inside the main range.
            var inside = LensMinimumX >= MinimumX && LensMaximumX <= MaximumX;

            _dragStart = new DragStart(
                at,
                plot,
                MinimumX,
                MaximumX,
                MinimumY,
                MaximumY,
                LensMinimumX,
                LensMaximumX,
                inside ? LensMinimumX : placement.SourceMinimum,
                inside ? LensMaximumX : placement.SourceMaximum,
                LensHorizontalPosition,
                plot.Width - placement.Bounds.Width);
            _drag = InteractionDrag.LensDrag;

            return true;
        }

        if (!_panEnabled)
        {
            return false;
        }

        _dragStart = new DragStart(at, plot, MinimumX, MaximumX, MinimumY, MaximumY, LensMinimumX, LensMaximumX, 0, 0, LensHorizontalPosition, 0);
        _drag = InteractionDrag.MainPan;

        return true;
    }

    /// <summary>Begins a drag and captures the mouse for it, as the left button's press does.</summary>
    /// <returns>Whether a drag began and holds the capture; a drag that cannot capture does not begin.</returns>
    internal bool StartDrag(Point at)
    {
        if (!TryBeginDrag(at))
        {
            return false;
        }

        if (!CaptureMouse())
        {
            _drag = InteractionDrag.None;
            _dragStart = default;

            return false;
        }

        return true;
    }

    /// <summary>Moves the drag in progress to a coordinate: the state is the snapshot plus the pointer's displacement.</summary>
    /// <remarks>
    /// <para>
    /// <b>From the start, every time.</b> The new ranges are computed from the drag's snapshot and the displacement from the
    /// press, not from the last position, so no rounding accumulates and a pointer back on the press point restores the
    /// snapshot exactly.
    /// </para>
    /// <para>
    /// <b>Pan.</b> The value under the press stays under the pointer: <c>X − Δx / width · span</c> and <c>Y + Δy / height ·
    /// span</c>, the plot and the spans those of the snapshot. Dragging down moves the content down.
    /// </para>
    /// <para>
    /// <b>Lens drag.</b> The lens moves with the pointer — its position by <c>Δx / travel</c>, held to 0..1 — and its source
    /// by the main X distance the pointer covered, <c>Δx / width · main span</c>, its span kept and held inside the main
    /// range by shifting it, never by cutting it. Vertical movement does nothing; the main ranges are not touched.
    /// </para>
    /// </remarks>
    internal void DragTo(Point at)
    {
        if (_drag == InteractionDrag.None || !double.IsFinite(at.X) || !double.IsFinite(at.Y))
        {
            return;
        }

        var start = _dragStart;
        var dx = at.X - start.Pointer.X;
        var dy = at.Y - start.Pointer.Y;

        if (_drag == InteractionDrag.MainPan)
        {
            var horizontal = (dx / start.Plot.Width) * (start.MaximumX - start.MinimumX);
            var vertical = (dy / start.Plot.Height) * (start.MaximumY - start.MinimumY);
            var changed = false;

            if (RangeInteraction.TryShift(start.MinimumX, start.MaximumX, -horizontal, out var left, out var right))
            {
                changed |= WriteRange(MinimumXProperty, MaximumXProperty, MaximumX, left, right);
            }

            if (RangeInteraction.TryShift(start.MinimumY, start.MaximumY, vertical, out var bottom, out var top))
            {
                changed |= WriteRange(MinimumYProperty, MaximumYProperty, MaximumY, bottom, top);
            }

            _processingCounters.Pans += changed ? 1 : 0;

            return;
        }

        double position, low, high;

        if (dx == 0)
        {
            // Back on the press point: the state the drag began from, exactly.
            position = start.LensPosition;
            low = start.LensMinimum;
            high = start.LensMaximum;
        }
        else
        {
            position = start.LensTravel > 0 ? Math.Clamp(start.LensPosition + (dx / start.LensTravel), 0, 1) : start.LensPosition;

            var shift = (dx / start.Plot.Width) * (start.MaximumX - start.MinimumX);

            if (!RangeInteraction.TryShiftInside(start.LensBasisMinimum, start.LensBasisMaximum, shift, MinimumX, MaximumX, out low, out high))
            {
                // The main range moved under the drag so the source no longer fits: the source stays, the lens still moves.
                low = LensMinimumX;
                high = LensMaximumX;
            }
        }

        var moved = position != LensHorizontalPosition;

        if (moved)
        {
            SetCurrentValue(LensHorizontalPositionProperty, position);
        }

        moved |= WriteRange(LensMinimumXProperty, LensMaximumXProperty, LensMaximumX, low, high);
        _processingCounters.LensDrags += moved ? 1 : 0;
    }

    /// <summary>Ends the drag in progress where it stands, and releases the mouse if the drag holds it.</summary>
    internal void EndDrag()
    {
        var had = _drag != InteractionDrag.None;

        // Cleared first, so the LostMouseCapture the release raises finds nothing to cancel.
        _drag = InteractionDrag.None;
        _dragStart = default;

        if (had && IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// Writes a range's two ends, in the order that keeps it ordered between the writes: the maximum first when the new
    /// minimum is at or above the current maximum.
    /// </summary>
    /// <returns>Whether either end changed.</returns>
    private bool WriteRange(DependencyProperty minimumProperty, DependencyProperty maximumProperty, double currentMaximum, double minimum, double maximum)
    {
        var oldMinimum = (double)GetValue(minimumProperty);

        if (oldMinimum == minimum && currentMaximum == maximum)
        {
            return false;
        }

        if (minimum >= currentMaximum)
        {
            SetCurrentValue(maximumProperty, maximum);
            SetCurrentValue(minimumProperty, minimum);
        }
        else
        {
            SetCurrentValue(minimumProperty, minimum);
            SetCurrentValue(maximumProperty, maximum);
        }

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Zooms when <see cref="IsZoomEnabled"/> is on and the pointer is over the plot or the lens; the event is marked
    /// handled only then, so with zoom off, outside the plot, or with Ctrl or Alt held, a surrounding scroll viewer still
    /// scrolls. An override should call the base implementation to keep zooming.
    /// </remarks>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!e.Handled && ApplyWheel(e.GetPosition(this), e.Delta, Keyboard.Modifiers))
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Begins a lens drag (<see cref="IsLensInteractionEnabled"/>) or a pan (<see cref="IsPanEnabled"/>) and captures the
    /// mouse for the duration of the drag; the event is marked handled only then. An override should call the base
    /// implementation to keep these gestures.
    /// </remarks>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (!e.Handled && StartDrag(e.GetPosition(this)))
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    /// <remarks>Ends a drag in progress at the release position and releases the mouse capture it took.</remarks>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_drag != InteractionDrag.None)
        {
            DragTo(e.GetPosition(this));
            EndDrag();
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Ends a drag when its capture is lost — taken by another element, the window deactivated, the control disabled,
    /// hidden or removed — where it stands.
    /// </remarks>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        if (_drag != InteractionDrag.None)
        {
            _drag = InteractionDrag.None;
            _dragStart = default;
        }
    }

    /// <summary>Follows a pointer move with the drag in progress, if any; called by <see cref="OnMouseMove"/>.</summary>
    /// <remarks>A move with the left button no longer down, or without the capture the drag took, ends the drag instead.</remarks>
    private void ContinueDrag(MouseEventArgs e, Point at)
    {
        if (_drag == InteractionDrag.None)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed || !IsMouseCaptured)
        {
            EndDrag();

            return;
        }

        DragTo(at);
    }
}
