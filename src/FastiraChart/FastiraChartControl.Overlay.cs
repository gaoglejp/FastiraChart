using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FastiraChart.Axes;
using FastiraChart.Interaction;
using FastiraChart.Lens;
using FastiraChart.Rendering;
using FastiraChart.Series;

namespace FastiraChart;

// <summary>
// The crosshair and the tooltip: the pointer state drawn, through the coordinate model every gesture reads.
// </summary>
// <remarks>
// <para>
// <b>One coordinate model.</b> The overlay is composed from <see cref="ResolvePointer"/> — the pointer inverse — and nothing
// else: the crosshair stands at the pointer's own coordinate, its view (the plot or the lens) is the region the inverse
// reports, its rectangle is the one that view is drawn in, and the tooltip states the values the inverse gives there. In
// the lens that is the lens X (the effective source) and the main Y, exactly as a lens zoom and a lens drag read them.
// </para>
// <para>
// <b>One layer, its own.</b> A pointer move marks the overlay and nothing else; the frame is composed when WPF next renders
// the overlay, once however many moves came first. No main layer and no lens is built, invalidated or laid out by a move,
// the plot does not move, and nothing is added to the visual tree. A change the overlay is drawn from — a range, the plot,
// the lens state, the font, the DPI, Foreground or Background — marks it where the dirty model hears of that change
// (<see cref="Invalidate"/> for whatever moves the axes or the lens, and the lens, font, brush and DPI callbacks), so the
// overlay is never stale; a change it is not drawn from — a series append, a reference line, a grid brush — never
// reaches it.
// </para>
// <para>
// <b>Display only.</b> The crosshair and the tooltip take no input and change nothing; they are independent of each other
// and of the zoom, pan and lens switches, so either can be shown on a chart that takes no gesture at all.
// </para>
// </remarks>
public partial class FastiraChartControl
{
    /// <summary>Identifies the <see cref="IsCrosshairVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsCrosshairVisibleProperty =
        DependencyProperty.Register(
            nameof(IsCrosshairVisible),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(false, OnOverlayVisibilityChanged));

    /// <summary>Identifies the <see cref="IsTooltipVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsTooltipVisibleProperty =
        DependencyProperty.Register(
            nameof(IsTooltipVisible),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(false, OnOverlayVisibilityChanged));

    /// <summary>How far the tooltip stands from the pointer, across and down, in DIP.</summary>
    internal const double TooltipOffset = 14;

    /// <summary>Space between the tooltip's border and its text, across, in DIP.</summary>
    internal const double TooltipPaddingX = 6;

    /// <summary>Space between the tooltip's border and its text, down, in DIP.</summary>
    internal const double TooltipPaddingY = 3;

    /// <summary>Fractional digits beyond those one DIP resolves: one more, so a device pixel at 150 % still reads.</summary>
    internal const int TooltipExtraDigits = 1;

    private InteractionLayer? _interactionLayer;
    private InteractionCounters _interactionCounters;
    private bool _interactionShown;
    private Pen? _overlayPen;
    private Brush? _overlayPenBrush;
    private FormattedText? _tooltipText;
    private TooltipTextKey _tooltipKey;

    /// <summary>What the measured tooltip text was made from: kept while all of it stands.</summary>
    private readonly record struct TooltipTextKey(string Text, Typeface? Typeface, double FontSize, Brush? Brush, double PixelsPerDip, CultureInfo? Culture);

    /// <summary>
    /// Gets or sets whether a crosshair follows the pointer over the plot and the lens. The default is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While the pointer is over the plot, a vertical and a horizontal line cross at it, spanning the plot and clipped to it:
    /// neither reaches an axis band. Over the lens they span the lens instead — one crosshair, in the view the pointer is
    /// in, never one in each. Outside the plot, after the pointer leaves the control, and where the chart has no values
    /// (a degenerate range, an inactive lens) nothing is drawn. Drawn with <see cref="System.Windows.Controls.Control.Foreground"/>,
    /// above the series and the lens and below the axes.
    /// </para>
    /// <para>
    /// Display only, independent of <see cref="IsTooltipVisible"/> and of every gesture switch. Showing it, and moving the
    /// pointer while it is shown, never moves the plot or rebuilds anything the chart draws. Like every property of the
    /// control it is set on the control's thread, and may be bound, styled or animated.
    /// </para>
    /// </remarks>
    public bool IsCrosshairVisible
    {
        get => (bool)GetValue(IsCrosshairVisibleProperty);
        set => SetValue(IsCrosshairVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether a tooltip beside the pointer states the values under it. The default is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Over the plot it reads <c>X: …</c> and <c>Y: …</c>, the main values under the pointer; over the lens,
    /// <c>Lens X: …</c> and <c>Y: …</c> — the lens's own X, from the range the lens shows, and the chart's Y, which the lens
    /// shares. Values are written as axis labels are, in the current culture and never in exponent form, to the digits the
    /// screen resolves there; the value itself is not rounded. The box stands beside the pointer and is kept inside the
    /// plot (inside the control when the plot is smaller than the box). Nothing is shown outside the plot, after the
    /// pointer leaves, or where the chart has no values.
    /// </para>
    /// <para>
    /// Display only, independent of <see cref="IsCrosshairVisible"/> and of every gesture switch; it takes no input, so it
    /// never stands between the pointer and the chart. Like every property of the control it is set on the control's
    /// thread, and may be bound, styled or animated.
    /// </para>
    /// </remarks>
    public bool IsTooltipVisible
    {
        get => (bool)GetValue(IsTooltipVisibleProperty);
        set => SetValue(IsTooltipVisibleProperty, value);
    }

    /// <summary>How many times the overlay was invalidated and composed.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, kept out of <see cref="Counters"/> and
    /// <see cref="LensCounters"/> so no earlier reading changes. Internal: it is not a public API.
    /// </remarks>
    internal InteractionCounters InteractionCounters => _interactionCounters;

    private static void OnOverlayVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FastiraChartControl)d).RefreshInteraction();

    /// <summary>The background the tooltip box is filled with is drawn from: a replacement reaches the overlay alone.</summary>
    private static void OnBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FastiraChartControl)d).RefreshInteraction();

    /// <summary>Marks the overlay, when it shows something or could: the one place it is marked.</summary>
    /// <remarks>
    /// Nothing is marked while neither the crosshair nor the tooltip is on and the last frame drew nothing, so with both
    /// off a pointer move still costs the pointer state's two stores and invalidates nothing. A frame that drew something is always
    /// composed again, so turning the overlay off, the pointer leaving or a change that takes its values away clears it.
    /// </remarks>
    private void RefreshInteraction()
    {
        if (_interactionLayer is { } layer && (_interactionShown || (_hasPointer && (IsCrosshairVisible || IsTooltipVisible))))
        {
            _interactionCounters.Invalidations++;
            layer.Invalidate();
        }
    }

    /// <summary>
    /// Composes the overlay from the pointer and the chart as they stand, for the interaction layer to draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called by the layer when it renders a stale frame, once per frame. The pointer is resolved here, by the pointer inverse,
    /// against <c>_tickPlotBounds</c> — the rectangle every layer was placed against — and the lens placed as the lens layer
    /// places it, so the crosshair's view is the one drawn and the tooltip's values are the ones the gestures read.
    /// </para>
    /// <para>
    /// Over the lens with lens values: the lens rectangle, the lens X and the Y. Over the plot with main values: the plot,
    /// the main X and Y. Anything else — no pointer, outside, a lens or a plot with no values — draws nothing.
    /// </para>
    /// </remarks>
    private InteractionFrame BuildInteraction()
    {
        _interactionCounters.Builds++;

        var crosshair = IsCrosshairVisible;
        var tooltip = IsTooltipVisible;

        if ((!crosshair && !tooltip) || !_hasPointer)
        {
            _interactionShown = false;

            return InteractionFrame.Empty;
        }

        var plot = _tickPlotBounds;
        var s = ResolvePointer(_pointerScreen);
        Rect bounds;
        ChartPoint value;
        double xPerDip;
        double yPerDip = (MaximumY - MinimumY) / plot.Height;

        if (s.IsInsideLens)
        {
            if (!s.HasLensData || !LensMapping.TryPlace(LensState, plot, MinimumX, MaximumX, out var placement))
            {
                _interactionShown = false;

                return InteractionFrame.Empty;
            }

            bounds = placement.Bounds;
            value = s.LensData;
            xPerDip = (placement.SourceMaximum - placement.SourceMinimum) / bounds.Width;
        }
        else if (s.IsInsidePlot && s.HasData)
        {
            bounds = plot;
            value = s.Data;
            xPerDip = (MaximumX - MinimumX) / plot.Width;
        }
        else
        {
            _interactionShown = false;

            return InteractionFrame.Empty;
        }

        var pen = OverlayPen();
        var text = string.Empty;
        FormattedText? measured = null;
        var box = Rect.Empty;
        var origin = default(Point);

        if (tooltip)
        {
            var culture = CultureInfo.CurrentCulture;

            text = (s.IsInsideLens ? "Lens X: " : "X: ")
                + AxisLabelFormatter.Format(value.X, TooltipDigits(xPerDip), culture)
                + "\nY: "
                + AxisLabelFormatter.Format(value.Y, TooltipDigits(yPerDip), culture);
            measured = TooltipText(text, culture);
            box = PlaceTooltip(s.Screen, plot, new Size(measured.WidthIncludingTrailingWhitespace + (2 * TooltipPaddingX), measured.Height + (2 * TooltipPaddingY)));
            origin = new Point(box.X + TooltipPaddingX, box.Y + TooltipPaddingY);
        }

        _interactionShown = true;

        return new InteractionFrame(crosshair, tooltip, s.Region, bounds, s.Screen, value, pen, text, measured, box, origin, Background);
    }

    /// <summary>
    /// How many fractional digits a value needs where one DIP spans <paramref name="perDip"/> of it: enough to tell one DIP
    /// from the next, and <see cref="TooltipExtraDigits"/> more, 0 to 10.
    /// </summary>
    internal static int TooltipDigits(double perDip)
    {
        if (!double.IsFinite(perDip) || !(perDip > 0))
        {
            return AxisLabelFormatter.MaximumFractionalDigits;
        }

        // The small allowance keeps an exact power of ten (0.1 per DIP) at its own digit count despite Log10's last bit.
        var digits = Math.Ceiling(-Math.Log10(perDip) - 1e-9) + TooltipExtraDigits;

        return (int)Math.Clamp(digits, 0, AxisLabelFormatter.MaximumFractionalDigits);
    }

    /// <summary>
    /// Places the tooltip box beside the pointer: below and right of it, flipped to the other side where it would leave
    /// its container, then held inside it.
    /// </summary>
    /// <remarks>
    /// The container is the plot; a box larger than the plot in either direction is held inside the control instead, and
    /// one larger than the control starts at its top left.
    /// </remarks>
    internal Rect PlaceTooltip(Point pointer, Rect plot, Size size)
    {
        var container = size.Width <= plot.Width && size.Height <= plot.Height ? plot : new Rect(RenderSize);

        static double Place(double at, double size, double low, double high)
        {
            var start = at + TooltipOffset;

            if (start + size > high)
            {
                start = at - TooltipOffset - size;
            }

            return size >= high - low ? low : Math.Clamp(start, low, high - size);
        }

        return new Rect(
            Place(pointer.X, size.Width, container.Left, container.Right),
            Place(pointer.Y, size.Height, container.Top, container.Bottom),
            size.Width,
            size.Height);
    }

    /// <summary>The tooltip text measured in the control's font and <see cref="System.Windows.Controls.Control.Foreground"/>, kept while all of it stands.</summary>
    /// <remarks>The text is the labels' own measurement (<see cref="AxisLabelMeasurement.Create"/>): the same typeface, size, brush, DPI and culture.</remarks>
    private FormattedText TooltipText(string text, CultureInfo culture)
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var brush = Foreground ?? Brushes.Transparent;
        var key = new TooltipTextKey(text, typeface, FontSize, brush, pixelsPerDip, culture);

        if (_tooltipText is { } kept && _tooltipKey == key)
        {
            _interactionCounters.TextHits++;

            return kept;
        }

        _tooltipText = AxisLabelMeasurement.Create(text, typeface, FontSize, brush, pixelsPerDip, culture);
        _tooltipKey = key;
        _interactionCounters.TextMeasurements++;

        return _tooltipText;
    }

    /// <summary>The pen the crosshair and the tooltip border are stroked with: <see cref="System.Windows.Controls.Control.Foreground"/>, 1 DIP.</summary>
    /// <remarks>
    /// Built as the tick pen is (see <see cref="TickPen"/>) — frozen over a frozen brush, live over a caller's mutable one,
    /// which is never frozen — but the overlay's own, so no tick cache count moves for it.
    /// </remarks>
    private Pen? OverlayPen()
    {
        var brush = Foreground;

        if (brush is null)
        {
            return null;
        }

        if (_overlayPen is not null && ReferenceEquals(_overlayPenBrush, brush))
        {
            return _overlayPen;
        }

        _overlayPenBrush = brush;
        _overlayPen = brush.IsFrozen ? FrozenPen(brush, 1) : new Pen(brush, 1);

        return _overlayPen;
    }

    /// <summary>Drops the overlay's pen and measured text, so the next composition makes both again.</summary>
    private void DropOverlayStyle()
    {
        _overlayPen = null;
        _overlayPenBrush = null;
        _tooltipText = null;
        _tooltipKey = default;
    }
}
