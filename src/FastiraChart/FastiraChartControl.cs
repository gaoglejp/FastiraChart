using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FastiraChart.Axes;
using FastiraChart.Gridlines;
using FastiraChart.Interaction;
using FastiraChart.Layout;
using FastiraChart.Lens;
using FastiraChart.ReferenceLines;
using FastiraChart.Rendering;
using FastiraChart.Series;

namespace FastiraChart;

/// <summary>
/// A WPF chart control: line series, reference and origin lines, a major grid and two value axes, with an optional lens,
/// zoom, pan, crosshair and tooltip.
/// </summary>
/// <remarks>
/// <para>
/// A standard WPF custom control: it derives from <see cref="Control"/>, is styled and templated through its default
/// style, and can be created from XAML or from code. In XAML, every public type is in one namespace:
/// <c>xmlns:fc="urn:fastirachart:wpf"</c>. The control requests no size of its own; it takes whatever size the host
/// layout gives it.
/// </para>
/// <para>
/// <b>Naming.</b> <c>X</c> and <c>Y</c> name a data (value) dimension — <see cref="MinimumX"/>,
/// <see cref="XAxisPosition"/>, <see cref="IsYAxisVisible"/>. <c>Horizontal</c> and <c>Vertical</c> name a screen
/// direction only — the way a line runs (<see cref="IsHorizontalMajorGridVisible"/> draws horizontal lines, at the Y
/// ticks) or where something sits (<see cref="LensHorizontalPosition"/>). <c>Top</c>, <c>Bottom</c>, <c>Left</c> and
/// <c>Right</c> name a screen edge.
/// </para>
/// <para>
/// <b>Ranges are the caller's.</b> The X range is <see cref="MinimumX"/> to <see cref="MaximumX"/>, the Y range
/// <see cref="MinimumY"/> to <see cref="MaximumY"/>, 0 to 100 by default. No range is ever derived from the data.
/// </para>
/// <para>
/// <b>Layout.</b> <see cref="PlotBounds"/> is the plot rectangle, in this control's own coordinates. It is computed from
/// the area inside the border, <see cref="Control.Padding"/>, <see cref="MinimumAxisSpace"/>, the room the labels need,
/// and the minimum plot (<see cref="MinimumPlotWidth"/>, <see cref="MinimumPlotHeight"/>,
/// <see cref="MinimumPlotRatio"/>), in that priority from last to first: padding is reduced first, then axis space, and
/// the minimum plot is kept.
/// </para>
/// <para>
/// <b>Interaction is off by default.</b> <see cref="IsZoomEnabled"/>, <see cref="IsPanEnabled"/>,
/// <see cref="IsLensInteractionEnabled"/>, <see cref="IsCrosshairVisible"/> and <see cref="IsTooltipVisible"/> are all
/// false: by default the chart takes no mouse input. The range and lens properties the gestures change bind two-way by
/// default.
/// </para>
/// <para>
/// <b>Caller-owned objects are never frozen or modified.</b> A brush, a <see cref="LineSeries"/>, a
/// <see cref="ChartReferenceLine"/> or a list of points the caller hands the control stays the caller's: the control
/// draws with frozen snapshots of mutable brushes and takes a new one when a brush changes. Only the library's own
/// default brushes are frozen. <see cref="Series"/> and <see cref="ReferenceLines"/> are created per instance and never
/// shared between controls.
/// </para>
/// <para>
/// <b>Custom templates are supported.</b> The template parts are seven optional panels, named by the
/// <c>…PartName</c> constants and listed below from bottom to top. A template supplies them as <b>siblings</b> in one
/// root panel that spans the whole control — not inside the border element, which would offset them by the border
/// thickness twice. All are optional: a template without a part draws that layer not at all, and never throws. Full
/// functionality needs all seven:
/// </para>
/// <list type="number">
/// <item><description><see cref="GridHostPartName"/> (<c>PART_GridHost</c>) — the major grid.</description></item>
/// <item><description><see cref="ReferenceLineHostPartName"/> (<c>PART_ReferenceLineHost</c>) — reference lines, then origin lines.</description></item>
/// <item><description><see cref="SeriesHostPartName"/> (<c>PART_SeriesHost</c>) — the series, in collection order.</description></item>
/// <item><description><see cref="ContentHostPartName"/> (<c>PART_ContentHost</c>) — a <see cref="FrameworkElement"/> arranged onto the plot, for the template author's own plot-aligned content.</description></item>
/// <item><description><see cref="LensHostPartName"/> (<c>PART_LensHost</c>) — the lens.</description></item>
/// <item><description><see cref="InteractionHostPartName"/> (<c>PART_InteractionHost</c>) — the crosshair and the tooltip.</description></item>
/// <item><description><see cref="AxisHostPartName"/> (<c>PART_AxisHost</c>) — tick marks, then labels.</description></item>
/// </list>
/// <para>
/// Every part except <see cref="ContentHostPartName"/> is a <see cref="Panel"/> the control adds one drawing element to;
/// those should not be hit-testable, so the drawing never takes the pointer from the chart. The template's border
/// element applies <see cref="Control.BorderThickness"/> but must not apply <see cref="Control.Padding"/>: the control
/// applies padding itself. A later version may add optional parts; it will not add a required one.
/// </para>
/// <para>
/// The class is not sealed: like any WPF control it may be derived from, restyled and retemplated. An override of a
/// WPF method it implements should call the base implementation.
/// </para>
/// </remarks>
[TemplatePart(Name = ContentHostPartName, Type = typeof(FrameworkElement))]
[TemplatePart(Name = AxisHostPartName, Type = typeof(Panel))]
[TemplatePart(Name = GridHostPartName, Type = typeof(Panel))]
[TemplatePart(Name = ReferenceLineHostPartName, Type = typeof(Panel))]
[TemplatePart(Name = SeriesHostPartName, Type = typeof(Panel))]
[TemplatePart(Name = LensHostPartName, Type = typeof(Panel))]
[TemplatePart(Name = InteractionHostPartName, Type = typeof(Panel))]
public partial class FastiraChartControl : Control
{
    /// <summary>
    /// Name of the template part that hosts the plot area.
    /// </summary>
    /// <remarks>
    /// The part is positioned and sized to <see cref="PlotBounds"/> during arrange, so
    /// its bounds and the plot area are the same rectangle by construction rather than
    /// by two calculations that could drift apart.
    /// </remarks>
    public const string ContentHostPartName = "PART_ContentHost";

    /// <summary>
    /// Name of the template part that hosts the axis tick visuals.
    /// </summary>
    /// <remarks>
    /// A panel spanning the whole control, into which the control puts a single drawing
    /// element for every tick. It is separate from <see cref="ContentHostPartName"/> because
    /// that part is clipped to the plot area, and ticks live outside it, in the reserved axis
    /// bands. A replacement template need not supply it; ticks are then simply not drawn.
    /// </remarks>
    public const string AxisHostPartName = "PART_AxisHost";

    /// <summary>
    /// Name of the template part that hosts the major grid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A panel spanning the whole control, into which the control puts a single drawing element for
    /// every grid line. It spans the control rather than the plot so the grid shares the coordinate
    /// space the ticks are placed in: a grid line and its tick are then computed by one expression,
    /// with no translation between them that could round differently.
    /// </para>
    /// <para>
    /// Its position in the template is its z-order: above the background, below
    /// <see cref="SeriesHostPartName"/>, where series draw, and below
    /// <see cref="AxisHostPartName"/>, so a grid line is never painted over a series, a tick or a label.
    /// A replacement template need not supply it; the grid is then simply not drawn.
    /// </para>
    /// </remarks>
    public const string GridHostPartName = "PART_GridHost";

    /// <summary>
    /// Name of the template part that hosts the origin and reference lines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A panel spanning the whole control, into which the control puts a single drawing element for
    /// every reference line and both origin lines. It spans the control for the reason
    /// <see cref="GridHostPartName"/> does: the lines are computed in the space the ticks and the grid
    /// are, by the same expressions, so a reference line at a tick's value lands exactly on its grid line.
    /// </para>
    /// <para>
    /// Its position in the template is its z-order: above <see cref="GridHostPartName"/>, so a reference
    /// or origin line is drawn over the grid line it may coincide with, and below
    /// <see cref="SeriesHostPartName"/>, where series draw, and <see cref="AxisHostPartName"/>.
    /// A replacement template need not supply it; origin and reference lines are then simply not drawn.
    /// </para>
    /// </remarks>
    public const string ReferenceLineHostPartName = "PART_ReferenceLineHost";

    /// <summary>
    /// Name of the template part that hosts the series.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A panel spanning the whole control, into which the control puts a single drawing element for every
    /// series and every point. It spans the control for the reason <see cref="GridHostPartName"/> does: a
    /// series point is computed in the space the ticks, the grid and the reference lines are, by the same
    /// expressions, so a point at a tick's value lands exactly on its grid line. It is not
    /// <see cref="ContentHostPartName"/>, which is arranged onto the plot and so has coordinates of its own.
    /// </para>
    /// <para>
    /// Its position in the template is its z-order: above <see cref="ReferenceLineHostPartName"/>, so a series
    /// is drawn over the grid, the reference lines and the origin lines, and below
    /// <see cref="ContentHostPartName"/> and <see cref="AxisHostPartName"/>, so no series is ever painted over
    /// a tick or a label. A replacement template need not supply it; series are then simply not drawn.
    /// </para>
    /// </remarks>
    public const string SeriesHostPartName = "PART_SeriesHost";

    /// <summary>
    /// Name of the template part that hosts the lens (see <see cref="IsLensVisible"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A panel spanning the whole control, into which the control puts a single drawing element for the lens. It spans
    /// the control for the reason <see cref="GridHostPartName"/> does: the lens is placed in the space the plot, the ticks
    /// and the series are, by the same expressions.
    /// </para>
    /// <para>
    /// Its position in the template is its z-order: above <see cref="ContentHostPartName"/> and everything beneath it —
    /// the grid, the reference and origin lines, the series — and below <see cref="AxisHostPartName"/>, so the lens covers
    /// the plot it magnifies and never a tick or a label. A replacement template need not supply it; no lens is then drawn.
    /// </para>
    /// </remarks>
    public const string LensHostPartName = "PART_LensHost";

    /// <summary>
    /// Name of the template part that hosts the crosshair and the tooltip (see <see cref="IsCrosshairVisible"/> and
    /// <see cref="IsTooltipVisible"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A panel spanning the whole control, into which the control puts a single drawing element for the crosshair and the
    /// tooltip together. It spans the control for the reason <see cref="GridHostPartName"/> does: the crosshair stands at
    /// the pointer's coordinate in the control's own space, the space the plot and the lens are placed in.
    /// </para>
    /// <para>
    /// Its position in the template is its z-order: above <see cref="LensHostPartName"/> and everything beneath it, so the
    /// crosshair is drawn over the series and the lens, and below <see cref="AxisHostPartName"/>, so a tick or a label is
    /// never covered. It should not be hit-testable, so the overlay never takes the pointer from the chart. A replacement
    /// template need not supply it; no crosshair or tooltip is then drawn, and every gesture still works.
    /// </para>
    /// </remarks>
    public const string InteractionHostPartName = "PART_InteractionHost";

    /// <summary>
    /// Length of a major tick mark, in device-independent pixels.
    /// </summary>
    /// <remarks>
    /// Deliberately a constant and not a dependency property. Tick length is styling, and a single
    /// tick-styling property would invite the dozen that a styling surface needs; the control keeps
    /// tick styling to the existing brush. The effective length is clamped to the axis space that
    /// was actually granted, so this is a request rather than a guarantee.
    /// </remarks>
    internal const double MajorTickLength = 6;

    // The four stroke brushes' defaults: the default style's colours, owned by FastiraChart and frozen, so a template or a
    // style that replaces the default one without basing itself on it still draws the grid, the origin and reference
    // lines and the series. The default style sets the same colours; with it, these are never read. Declared before the
    // dependency properties whose metadata they are, because static fields are initialized in this order.

    /// <summary>Default <see cref="MajorGridBrush"/>: <c>#FF34343E</c>, frozen.</summary>
    internal static readonly Brush DefaultMajorGridBrush = FrozenBrush(0xFF, 0x34, 0x34, 0x3E);

    /// <summary>Default <see cref="OriginLineBrush"/>: <c>#FF6E6E7C</c>, frozen.</summary>
    internal static readonly Brush DefaultOriginLineBrush = FrozenBrush(0xFF, 0x6E, 0x6E, 0x7C);

    /// <summary>Default <see cref="ReferenceLineBrush"/>: <c>#FFB08A4E</c>, frozen.</summary>
    internal static readonly Brush DefaultReferenceLineBrush = FrozenBrush(0xFF, 0xB0, 0x8A, 0x4E);

    /// <summary>Default <see cref="SeriesStroke"/>: <c>#FF4FA3E8</c>, frozen.</summary>
    internal static readonly Brush DefaultSeriesStroke = FrozenBrush(0xFF, 0x4F, 0xA3, 0xE8);

    private static readonly DependencyPropertyKey PlotBoundsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PlotBounds),
            typeof(Rect),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(new Rect()));

    private static readonly DependencyPropertyKey EffectivePaddingPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(EffectivePadding),
            typeof(Thickness),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(new Thickness()));

    private static readonly DependencyPropertyKey EffectiveAxisSpacePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(EffectiveAxisSpace),
            typeof(Thickness),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(new Thickness()));

    private static readonly DependencyPropertyKey RequiredAxisSpacePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(RequiredAxisSpace),
            typeof(Thickness),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(new Thickness()));

    /// <summary>Identifies the <see cref="PlotBounds"/> read-only dependency property.</summary>
    public static readonly DependencyProperty PlotBoundsProperty =
        PlotBoundsPropertyKey.DependencyProperty;

    // The three layout diagnostics below are internal: what the resolver granted and what the labels asked for. They are
    // read by internal validation, not published — PlotBounds is the one layout output a host consumes.

    /// <summary>Identifies the <see cref="EffectivePadding"/> read-only dependency property.</summary>
    internal static readonly DependencyProperty EffectivePaddingProperty =
        EffectivePaddingPropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="EffectiveAxisSpace"/> read-only dependency property.</summary>
    internal static readonly DependencyProperty EffectiveAxisSpaceProperty =
        EffectiveAxisSpacePropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="RequiredAxisSpace"/> read-only dependency property.</summary>
    internal static readonly DependencyProperty RequiredAxisSpaceProperty =
        RequiredAxisSpacePropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="MinimumAxisSpace"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumAxisSpaceProperty =
        DependencyProperty.Register(
            nameof(MinimumAxisSpace),
            typeof(Thickness),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(new Thickness(), FrameworkPropertyMetadataOptions.AffectsArrange),
            IsValidAxisSpace);

    /// <summary>Identifies the <see cref="MinimumX"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumXProperty =
        DependencyProperty.Register(
            nameof(MinimumX),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged),
            IsFiniteDouble);

    /// <summary>Identifies the <see cref="MaximumX"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumXProperty =
        DependencyProperty.Register(
            nameof(MaximumX),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultRangeMaximum, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged),
            IsFiniteDouble);

    /// <summary>Identifies the <see cref="MinimumY"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumYProperty =
        DependencyProperty.Register(
            nameof(MinimumY),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged),
            IsFiniteDouble);

    /// <summary>Identifies the <see cref="MaximumY"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumYProperty =
        DependencyProperty.Register(
            nameof(MaximumY),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultRangeMaximum, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged),
            IsFiniteDouble);

    /// <summary>Identifies the <see cref="XAxisTargetTickCount"/> dependency property.</summary>
    public static readonly DependencyProperty XAxisTargetTickCountProperty =
        DependencyProperty.Register(
            nameof(XAxisTargetTickCount),
            typeof(int),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultTargetTickCount, OnTargetTickCountChanged),
            IsValidTargetTickCount);

    /// <summary>Identifies the <see cref="YAxisTargetTickCount"/> dependency property.</summary>
    public static readonly DependencyProperty YAxisTargetTickCountProperty =
        DependencyProperty.Register(
            nameof(YAxisTargetTickCount),
            typeof(int),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultTargetTickCount, OnTargetTickCountChanged),
            IsValidTargetTickCount);

    /// <summary>Identifies the <see cref="XAxisPosition"/> dependency property.</summary>
    public static readonly DependencyProperty XAxisPositionProperty =
        DependencyProperty.Register(
            nameof(XAxisPosition),
            typeof(HorizontalAxisPosition),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(HorizontalAxisPosition.Bottom, OnAxisPositionChanged),
            IsDefinedHorizontalAxisPosition);

    /// <summary>Identifies the <see cref="YAxisPosition"/> dependency property.</summary>
    public static readonly DependencyProperty YAxisPositionProperty =
        DependencyProperty.Register(
            nameof(YAxisPosition),
            typeof(VerticalAxisPosition),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(VerticalAxisPosition.Left, OnAxisPositionChanged),
            IsDefinedVerticalAxisPosition);

    /// <summary>Identifies the <see cref="IsXAxisVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsXAxisVisibleProperty =
        DependencyProperty.Register(
            nameof(IsXAxisVisible),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(true, OnAxisVisibilityChanged));

    /// <summary>Identifies the <see cref="IsYAxisVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsYAxisVisibleProperty =
        DependencyProperty.Register(
            nameof(IsYAxisVisible),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(true, OnAxisVisibilityChanged));

    /// <summary>Identifies the <see cref="XAxisLabelRotation"/> dependency property.</summary>
    public static readonly DependencyProperty XAxisLabelRotationProperty =
        DependencyProperty.Register(
            nameof(XAxisLabelRotation),
            typeof(LabelRotation),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(LabelRotation.Degree0, FrameworkPropertyMetadataOptions.AffectsArrange, OnLabelShapeChanged),
            IsDefinedLabelRotation);

    /// <summary>Identifies the <see cref="YAxisLabelRotation"/> dependency property.</summary>
    public static readonly DependencyProperty YAxisLabelRotationProperty =
        DependencyProperty.Register(
            nameof(YAxisLabelRotation),
            typeof(LabelRotation),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(LabelRotation.Degree0, FrameworkPropertyMetadataOptions.AffectsArrange, OnLabelShapeChanged),
            IsDefinedLabelRotation);

    /// <summary>Identifies the <see cref="XAxisLabelAnchor"/> dependency property.</summary>
    public static readonly DependencyProperty XAxisLabelAnchorProperty =
        DependencyProperty.Register(
            nameof(XAxisLabelAnchor),
            typeof(LabelAnchor),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(LabelAnchor.Center, FrameworkPropertyMetadataOptions.AffectsArrange, OnLabelShapeChanged),
            IsDefinedLabelAnchor);

    /// <summary>Identifies the <see cref="YAxisLabelAnchor"/> dependency property.</summary>
    public static readonly DependencyProperty YAxisLabelAnchorProperty =
        DependencyProperty.Register(
            nameof(YAxisLabelAnchor),
            typeof(LabelAnchor),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(LabelAnchor.Center, FrameworkPropertyMetadataOptions.AffectsArrange, OnLabelShapeChanged),
            IsDefinedLabelAnchor);

    /// <summary>Identifies the <see cref="LabelGap"/> dependency property.</summary>
    public static readonly DependencyProperty LabelGapProperty =
        DependencyProperty.Register(
            nameof(LabelGap),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultLabelGap, FrameworkPropertyMetadataOptions.AffectsArrange, OnLabelShapeChanged),
            IsValidLabelGap);

    /// <summary>Identifies the <see cref="IsHorizontalMajorGridVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsHorizontalMajorGridVisibleProperty =
        DependencyProperty.Register(
            nameof(IsHorizontalMajorGridVisible),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(true, OnGridInputChanged));

    /// <summary>Identifies the <see cref="IsVerticalMajorGridVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsVerticalMajorGridVisibleProperty =
        DependencyProperty.Register(
            nameof(IsVerticalMajorGridVisible),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(true, OnGridInputChanged));

    /// <summary>Identifies the <see cref="MajorGridBrush"/> dependency property.</summary>
    public static readonly DependencyProperty MajorGridBrushProperty =
        DependencyProperty.Register(
            nameof(MajorGridBrush),
            typeof(Brush),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultMajorGridBrush, OnGridInputChanged));

    /// <summary>Identifies the <see cref="MajorGridThickness"/> dependency property.</summary>
    public static readonly DependencyProperty MajorGridThicknessProperty =
        DependencyProperty.Register(
            nameof(MajorGridThickness),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultMajorGridThickness, OnGridInputChanged),
            IsValidGridThickness);

    /// <summary>Identifies the <see cref="IsHorizontalOriginLineVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsHorizontalOriginLineVisibleProperty =
        DependencyProperty.Register(
            nameof(IsHorizontalOriginLineVisible),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(true, OnOriginStyleChanged));

    /// <summary>Identifies the <see cref="IsVerticalOriginLineVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsVerticalOriginLineVisibleProperty =
        DependencyProperty.Register(
            nameof(IsVerticalOriginLineVisible),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(true, OnOriginStyleChanged));

    /// <summary>Identifies the <see cref="OriginLineBrush"/> dependency property.</summary>
    public static readonly DependencyProperty OriginLineBrushProperty =
        DependencyProperty.Register(
            nameof(OriginLineBrush),
            typeof(Brush),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultOriginLineBrush, OnOriginStyleChanged));

    /// <summary>Identifies the <see cref="OriginLineThickness"/> dependency property.</summary>
    public static readonly DependencyProperty OriginLineThicknessProperty =
        DependencyProperty.Register(
            nameof(OriginLineThickness),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultOriginLineThickness, OnOriginStyleChanged),
            IsValidGridThickness);

    /// <summary>Identifies the <see cref="ReferenceLineBrush"/> dependency property.</summary>
    public static readonly DependencyProperty ReferenceLineBrushProperty =
        DependencyProperty.Register(
            nameof(ReferenceLineBrush),
            typeof(Brush),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultReferenceLineBrush, OnReferenceLineBrushChanged));

    /// <summary>Identifies the <see cref="SeriesStroke"/> dependency property.</summary>
    public static readonly DependencyProperty SeriesStrokeProperty =
        DependencyProperty.Register(
            nameof(SeriesStroke),
            typeof(Brush),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultSeriesStroke, OnSeriesStrokeChanged));

    private static readonly DependencyPropertyKey SeriesPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Series),
            typeof(FreezableCollection<LineSeries>),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(null, OnSeriesChanged));

    /// <summary>Identifies the <see cref="Series"/> read-only dependency property.</summary>
    public static readonly DependencyProperty SeriesProperty =
        SeriesPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ReferenceLinesPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ReferenceLines),
            typeof(FreezableCollection<ChartReferenceLine>),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(null, OnReferenceLinesChanged));

    /// <summary>Identifies the <see cref="ReferenceLines"/> read-only dependency property.</summary>
    public static readonly DependencyProperty ReferenceLinesProperty =
        ReferenceLinesPropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="MinimumPlotWidth"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumPlotWidthProperty =
        DependencyProperty.Register(
            nameof(MinimumPlotWidth),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsArrange),
            IsValidMinimumExtent);

    /// <summary>Identifies the <see cref="MinimumPlotHeight"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumPlotHeightProperty =
        DependencyProperty.Register(
            nameof(MinimumPlotHeight),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsArrange),
            IsValidMinimumExtent);

    /// <summary>Identifies the <see cref="MinimumPlotRatio"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumPlotRatioProperty =
        DependencyProperty.Register(
            nameof(MinimumPlotRatio),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultMinimumPlotRatio, FrameworkPropertyMetadataOptions.AffectsArrange),
            IsValidRatio);

    /// <summary>Identifies the <see cref="ClipToPlotArea"/> dependency property.</summary>
    public static readonly DependencyProperty ClipToPlotAreaProperty =
        DependencyProperty.Register(
            nameof(ClipToPlotArea),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(true, OnClipToPlotAreaChanged));

    // The lens state. Validated where it is written, as every input of this control
    // is. None carries a layout flag — the lens takes no part in the layout — and every one marks the lens layer and
    // nothing else.

    /// <summary>Identifies the <see cref="IsLensVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsLensVisibleProperty =
        DependencyProperty.Register(
            nameof(IsLensVisible),
            typeof(bool),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(false, OnLensStateChanged));

    /// <summary>Identifies the <see cref="LensMinimumX"/> dependency property.</summary>
    public static readonly DependencyProperty LensMinimumXProperty =
        DependencyProperty.Register(
            nameof(LensMinimumX),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultLensMinimumX, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLensStateChanged),
            IsFiniteDouble);

    /// <summary>Identifies the <see cref="LensMaximumX"/> dependency property.</summary>
    public static readonly DependencyProperty LensMaximumXProperty =
        DependencyProperty.Register(
            nameof(LensMaximumX),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultLensMaximumX, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLensStateChanged),
            IsFiniteDouble);

    /// <summary>Identifies the <see cref="LensWidth"/> dependency property.</summary>
    public static readonly DependencyProperty LensWidthProperty =
        DependencyProperty.Register(
            nameof(LensWidth),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultLensWidth, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLensStateChanged),
            IsValidMinimumExtent);

    /// <summary>Identifies the <see cref="LensHorizontalPosition"/> dependency property.</summary>
    public static readonly DependencyProperty LensHorizontalPositionProperty =
        DependencyProperty.Register(
            nameof(LensHorizontalPosition),
            typeof(double),
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(DefaultLensHorizontalPosition, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLensStateChanged),
            IsValidRatio);

    /// <summary>Initial lens source minimum: the default range's middle fifth starts here.</summary>
    private const double DefaultLensMinimumX = 40;

    /// <summary>Initial lens source maximum.</summary>
    private const double DefaultLensMaximumX = 60;

    /// <summary>Initial lens display width, in DIP.</summary>
    private const double DefaultLensWidth = 200;

    /// <summary>Initial lens position: centred.</summary>
    private const double DefaultLensHorizontalPosition = 0.5;

    /// <summary>
    /// Initial recommended value for <see cref="MinimumPlotRatio"/>.
    /// </summary>
    /// <remarks>
    /// Leaves most of the control to the plot while keeping room for the axes and padding.
    /// </remarks>
    private const double DefaultMinimumPlotRatio = 0.80;

    /// <summary>Initial stroke thickness of a major grid line, in DIP.</summary>
    /// <remarks>
    /// One DIP: the thinnest line that stays a line at every scaling factor. Not rounded to a device
    /// pixel — at 150% it covers one and a half — because snapping it would move it off the tick it
    /// was computed from.
    /// </remarks>
    private const double DefaultMajorGridThickness = 1;

    /// <summary>Initial stroke thickness of an origin line, in DIP.</summary>
    /// <remarks>
    /// Half again a grid line, so the line that means zero reads as heavier than the grid lines around
    /// it — including the 0 grid line it lies on — without being mistaken for an axis. Not rounded to a
    /// device pixel, for the reason given on the grid thickness.
    /// </remarks>
    private const double DefaultOriginLineThickness = 1.5;

    /// <summary>Initial maximum for both axis ranges.</summary>
    /// <remarks>
    /// A neutral 0 to 100 so the control shows a sensible axis before anything is bound. Series are
    /// drawn against the range as set: no range is derived from the data.
    /// </remarks>
    private const double DefaultRangeMaximum = 100;

    /// <summary>Initial value for both target tick counts.</summary>
    /// <remarks>
    /// Six reads comfortably at typical chart sizes. It is a target for choosing the interval,
    /// not a quota: the actual count follows from where the nice step lands.
    /// </remarks>
    private const int DefaultTargetTickCount = 6;

    /// <summary>Initial gap between a tick's end and its label.</summary>
    /// <remarks>
    /// Four DIP separates the two clearly without pushing the label off its tick.
    /// </remarks>
    private const double DefaultLabelGap = 4;

    /// <summary>Largest accepted target tick count.</summary>
    /// <remarks>
    /// Well above anything legible, and far below the generator's own cap. It exists so that a
    /// mistaken binding is rejected where it is written rather than absorbed silently.
    /// </remarks>
    private const int MaximumTargetTickCount = 1000;

    static FastiraChartControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(typeof(FastiraChartControl)));

        // Padding participates in the plot calculation, so a change to it must re-arrange.
        // Control does not mark Padding as affecting arrange on its own.
        PaddingProperty.OverrideMetadata(
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(
                new Thickness(),
                FrameworkPropertyMetadataOptions.AffectsArrange));

        // BorderThickness is the other inherited input to the plot calculation: ArrangeOverride
        // subtracts it to get the available area. Control marks it AffectsMeasure only, which
        // re-runs MeasureOverride but leaves the arrange valid when the desired size does not
        // change — and this control is stretched, so its desired size never does. The result was
        // that PlotBounds, the content host's position and every tick kept the previous border's
        // values until something else happened to invalidate arrange.
        //
        // Measured on hardware rather than inferred: with the border driven 2, 3, 4, three
        // samples taken after layout had settled all reported the bounds computed for border 1,
        // the plot rectangle identical in all three.
        //
        // Both flags are stated rather than relying on the base metadata to supply AffectsMeasure
        // through the merge. The border still changes how the control measures.
        BorderThicknessProperty.OverrideMetadata(
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata(
                new Thickness(),
                FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange));

        // The font properties became layout inputs the moment labels did: a larger font measures
        // wider, which changes the axis thickness the labels need, which changes the plot
        // rectangle. Control marks them AffectsMeasure, which is not enough here for the same
        // reason it was not enough for BorderThickness — a stretched control's desired size never
        // changes, so the arrange stays valid and the measurements it was built from go stale.
        //
        // Registered through a loop rather than five near-identical blocks, so a property cannot
        // be added to the list and left half-wired. LX1 asserts the resulting metadata rather
        // than trusting this code to be complete.
        //
        // The metadata is built with the parameterless constructor and its flags set afterwards,
        // which is the only form that adds flags without also asserting a default value. There is
        // no FrameworkPropertyMetadata(FrameworkPropertyMetadataOptions, PropertyChangedCallback)
        // overload; passing the flags first binds to (object defaultValue, PropertyChangedCallback)
        // instead, boxing the enum as the default value. That compiles without a warning and then
        // throws at type-initialization, because a FrameworkPropertyMetadataOptions is not a
        // FontFamily. Leaving the default unset is also what is wanted here: these properties
        // keep whatever default Control gave them, and this override adds flags and nothing else.
        foreach (var property in FontProperties)
        {
            var metadata = new FrameworkPropertyMetadata
            {
                AffectsMeasure = true,
                AffectsArrange = true,
                PropertyChangedCallback = OnFontChanged,
            };

            property.OverrideMetadata(typeof(FastiraChartControl), metadata);
        }

        // The brush changes no geometry, so it must not invalidate layout. The measured text carries
        // it, and is re-coloured in place rather than measured again. Same construction,
        // same reason.
        ForegroundProperty.OverrideMetadata(
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata
            {
                AffectsRender = true,
                PropertyChangedCallback = OnForegroundChanged,
            });

        // The tooltip box is filled with Background; the template's border paints it by a template binding
        // and no layer was drawn from it until now. A callback only: no flag, no default — the overlay alone is marked.
        BackgroundProperty.OverrideMetadata(
            typeof(FastiraChartControl),
            new FrameworkPropertyMetadata
            {
                PropertyChangedCallback = OnBackgroundChanged,
            });
    }

    /// <summary>
    /// Every inherited font property label measurement depends on.
    /// </summary>
    /// <remarks>
    /// The list the static constructor widens and the one the validation suite checks, so the
    /// two cannot disagree about what "every font property" means.
    /// </remarks>
    internal static readonly DependencyProperty[] FontProperties =
    [
        FontFamilyProperty,
        FontSizeProperty,
        FontStyleProperty,
        FontWeightProperty,
        FontStretchProperty,
    ];

    /// <summary>
    /// Every property that changes label geometry without changing the text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These need no measurement invalidation, which is worth stating because the obvious assumption
    /// is the opposite. Rotation, anchor and gap move a label; they do not change what it says, and
    /// the measured text is what that cache holds. Only the tick values, the font, the culture and the
    /// DPI can change that — not the brush, which is applied to the measured text in
    /// place. They do drop the placements, which they move.
    /// </para>
    /// <para>
    /// They do all change the axis thickness the labels ask for, so every one carries
    /// <see cref="FrameworkPropertyMetadataOptions.AffectsArrange"/> — asserted by the validation
    /// suite against this list rather than taken on trust.
    /// </para>
    /// </remarks>
    internal static readonly DependencyProperty[] LabelShapeProperties =
    [
        XAxisLabelRotationProperty,
        YAxisLabelRotationProperty,
        XAxisLabelAnchorProperty,
        YAxisLabelAnchorProperty,
        LabelGapProperty,
    ];

    /// <summary>Re-measures the labels when a font property changes.</summary>
    /// <remarks>
    /// <para>
    /// The labels are redrawn in the new font whether or not the plot moves: the font flags ask WPF for the
    /// arrange that decides that, and an arrange that leaves the plot where it was marks no layer, so the
    /// axis is marked here.
    /// </para>
    /// <para>
    /// The text is measured again and the labels placed again (the measurement and the placements
    /// are dropped, nothing else); the tick marks, which no font reaches, keep their geometry unless the arrange
    /// moves the axis bands.
    /// </para>
    /// </remarks>
    private static void OnFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        control.InvalidateLabelCache();

        // The lens labels are measured in the same font, and the tooltip text.
        control.InvalidateLensLabels();
        control.Recompose(ChartLayers.Axis | ChartLayers.Lens);
        control.RefreshInteraction();
    }

    /// <summary>Redraws ticks and labels in the new brush; nothing else is drawn from it.</summary>
    /// <remarks>
    /// <para>
    /// A style change, and nothing more. The tick pen is made again over the new brush, and the
    /// measured label text — which carries the brush it is drawn with — is handed the new brush in place
    /// (<see cref="FormattedText.SetForegroundBrush(Brush)"/>), so no label is measured or placed again and no tick
    /// mark is placed again.
    /// </para>
    /// <para>
    /// Called for a replacement and for a change inside a mutable brush set on this control alike. The pen is
    /// dropped rather than compared: a replacement may swap a frozen brush for a mutable one or back, which changes
    /// how the pen must be built. The caller's brush is never frozen either way; see <see cref="TickPen"/>.
    /// </para>
    /// </remarks>
    private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        control._tickPen = null;
        control._labelStyleStale = true;

        // The lens's tick marks take the main tick pen, and its labels are re-coloured in place, as the main labels are
        //: nothing of the lens is measured or placed again.
        control._lensLabelStyleStale = true;
        control.Recompose(ChartLayers.Axis | ChartLayers.Lens);

        // The crosshair, the tooltip's border and its text are drawn with it too.
        control.RefreshInteraction();
    }

    /// <summary>Re-places the labels when a rotation, an anchor or the gap changes.</summary>
    /// <remarks>
    /// These carry <see cref="FrameworkPropertyMetadataOptions.AffectsArrange"/> as well, because they change
    /// the axis thickness the labels ask for. When that moves the plot, the arrange marks every layer; when the
    /// requested <see cref="MinimumAxisSpace"/> already covers it and the plot stays, only the labels moved, and only
    /// the axis is rebuilt.
    /// <para>
    /// Only the placements are dropped, and for a rotation or an anchor the labels' outward reach the
    /// layout reads: the measured text and the tick marks are reused.
    /// </para>
    /// </remarks>
    private static void OnLabelShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        // A horizontal rotation or anchor moves the horizontal axis's labels alone, a vertical one the vertical axis's
        //; the gap moves both.
        var horizontal = e.Property == LabelGapProperty || e.Property == XAxisLabelRotationProperty || e.Property == XAxisLabelAnchorProperty;
        var vertical = e.Property == LabelGapProperty || e.Property == YAxisLabelRotationProperty || e.Property == YAxisLabelAnchorProperty;

        if (horizontal)
        {
            control._horizontalLabelPlacements = null;
        }

        if (vertical)
        {
            control._verticalLabelPlacements = null;
        }

        // The gap is added to the reach, not part of it: RequiredAxisThickness takes the two separately.
        if (e.Property != LabelGapProperty)
        {
            if (horizontal)
            {
                control._horizontalLabelExtent = null;
            }

            if (vertical)
            {
                control._verticalLabelExtent = null;
            }
        }

        // The lens's labels are the horizontal axis's: a horizontal rotation or anchor, or the gap, places them
        // again; a vertical one does not reach the lens.
        if (horizontal)
        {
            control._lensLabelPlacements = null;
        }

        control.Recompose(ChartLayers.Axis | (horizontal ? ChartLayers.Lens : ChartLayers.None));
    }

    private AxisTickLayer? _tickLayer;
    private AxisLabelLayer? _labelLayer;
    private MajorGridLayer? _gridLayer;
    private ReferenceLineLayer? _referenceLayer;
    private SeriesLayer? _seriesLayer;
    private LensLayer? _lensLayer;
    private RenderCounters _counters;
    private CacheCounters _cacheCounters;
    private LensCounters _lensCounters;
    private ProcessingCounters _processingCounters;

    // The lens's horizontal mapping: its rectangle and effective source, kept until the lens state, the plot
    // or the main horizontal range changes. The vertical half is the main chart's and is not kept here.
    private LensPart? _lensPart;
    private Dictionary<(Brush Brush, double Thickness), Pen> _seriesPens = [];
    private Dictionary<LineSeries, SeriesEntry> _seriesEntries = [];
    private int _seriesEpoch;
    private Pen? _originPen;
    private bool _originPenValid;

    // Every geometry cache below is kept in two halves, one per value axis: what is drawn from the
    // horizontal axis (X — vertical screen lines, the bottom or top axis) and what is drawn from the vertical one (Y —
    // horizontal screen lines, the left or right axis). A change reaching one axis drops that half alone; see
    // ValueAxes and Invalidate.
    private OriginPart? _verticalOrigin;
    private OriginPart? _horizontalOrigin;
    private Dictionary<(Brush Brush, double Thickness), Pen> _referencePens = [];
    private Dictionary<ChartReferenceLine, ReferenceEntry> _referenceEntries = [];
    private int _referenceEpochX;
    private int _referenceEpochY;
    private List<GridLine>? _verticalGridLines;
    private List<GridLine>? _horizontalGridLines;
    private IReadOnlyList<TickMark>? _horizontalTickMarks;
    private IReadOnlyList<TickMark>? _verticalTickMarks;
    private IReadOnlyList<PlacedLabel>? _horizontalLabelPlacements;
    private IReadOnlyList<PlacedLabel>? _verticalLabelPlacements;
    private IReadOnlyList<PlacedLabel>? _joinedLabels;
    private IReadOnlyList<TickMark>? _joinedTickMarks;
    private bool _labelStyleStale;
    private LabelMetrics[] _horizontalLabelMetrics = [];
    private LabelMetrics[] _verticalLabelMetrics = [];
    private double? _horizontalLabelExtent;
    private double? _verticalLabelExtent;
    private Pen? _gridPen;
    private Brush? _gridPenBrush;
    private double _gridPenThickness;
    private IReadOnlyList<FormattedText> _horizontalLabelTexts = [];
    private IReadOnlyList<FormattedText> _verticalLabelTexts = [];

    // The style each axis's text was shaped in, with the brush every one of its texts carries now: a rebuild
    // may take a string's shaped text from the last build of the same axis only in exactly this style. A re-colour hands
    // the texts a new brush and records it here. Null before the first build.
    private LabelTextStyle? _horizontalLabelStyle;
    private LabelTextStyle? _verticalLabelStyle;
    private LabelTextStyle? _lensLabelStyle;
    private bool _horizontalLabelTextsDirty = true;
    private bool _verticalLabelTextsDirty = true;
    private Rect _tickPlotBounds;
    private Thickness _tickAxisSpace;
    private IReadOnlyList<Tick>? _horizontalTicks;
    private IReadOnlyList<Tick>? _verticalTicks;
    private (double Minimum, double Maximum, int Target) _horizontalKey;
    private (double Minimum, double Maximum, int Target) _verticalKey;
    private Pen? _tickPen;
    private Brush? _tickPenBrush;

    /// <summary>
    /// Initializes a new instance of the <see cref="FastiraChartControl"/> class.
    /// </summary>
    public FastiraChartControl()
    {
        // Layout is driven entirely by the WPF measure/arrange pass, so the constructor subscribes
        // to no events. Its one allocation is this instance's own reference line
        // collection: created here rather than as a property default, because a default value is
        // shared by every instance, and one mutable collection behind every chart in an application
        // is exactly the defect to avoid. A change inside the collection reaches this control as a
        // sub-property change of the property it is held in, so nothing has to be subscribed or
        // later removed.
        SetValue(ReferenceLinesPropertyKey, new FreezableCollection<ChartReferenceLine>());

        // The series collection, for the same reasons: one per instance, and no subscription.
        SetValue(SeriesPropertyKey, new FreezableCollection<LineSeries>());
    }

    /// <summary>
    /// Gets the plot area — the rectangle data is drawn in — in device-independent pixels, in this control's own
    /// local coordinate space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Coordinate space.</b> Relative to this control's top-left corner: the space of
    /// <c>Mouse.GetPosition(chart)</c>, of <see cref="UIElement.TranslatePoint"/> to and from the chart, and of
    /// every layer the chart draws. The border is included, so with the default style's <c>BorderThickness</c> of 1
    /// the plot starts at least one DIP in from the control's edge. An element placed at
    /// (<see cref="Rect.Left"/>, <see cref="Rect.Top"/>) in a panel that shares the chart's origin lies exactly on the
    /// plot's top-left corner, at every border thickness and display scale.
    /// </para>
    /// <para>
    /// Computed during arrange from the size the control is given, <see cref="Control.BorderThickness"/>,
    /// <see cref="Control.Padding"/>, <see cref="MinimumAxisSpace"/>, the room the axis labels need and the minimum
    /// plot constraints, in this priority: the available area first, then the minimum plot
    /// (<see cref="MinimumPlotWidth"/>, <see cref="MinimumPlotHeight"/>, <see cref="MinimumPlotRatio"/>), then axis
    /// space, then padding — padding is reduced first when the area is too small. Always finite, with non-negative
    /// width and height. When there is no usable space it is a zero-sized rectangle — never
    /// <see cref="Rect.Empty"/>, which WPF defines with infinite position and negative extents. The default before
    /// the first arrange is (0, 0, 0, 0).
    /// </para>
    /// <para>
    /// Read-only: a computed result. It is a dependency property, so it can be bound to (one way) and observed. It
    /// describes geometry only; it is not pointer state.
    /// </para>
    /// </remarks>
    public Rect PlotBounds => (Rect)GetValue(PlotBoundsProperty);

    /// <summary>
    /// The plot area relative to the area inside the border: what the layout calculator returns, before the border
    /// offset that places it in this control's space (<see cref="PlotBounds"/>).
    /// </summary>
    /// <remarks>
    /// Internal: the layout's own result, kept for internal validation, whose layout checks are written against the
    /// calculator's space. Nothing draws from it; every layer draws in the control's space.
    /// </remarks>
    internal Rect LayoutPlotBounds { get; private set; }

    /// <summary>
    /// The padding actually applied, which may be smaller than <see cref="Control.Padding"/> when the minimum plot
    /// constraints required it.
    /// </summary>
    /// <remarks>
    /// Internal layout diagnostics for internal validation. The requested <see cref="Control.Padding"/> is never
    /// modified.
    /// </remarks>
    internal Thickness EffectivePadding => (Thickness)GetValue(EffectivePaddingProperty);

    /// <summary>
    /// Gets or sets the smallest space, in DIP, to reserve on each edge for axis ticks and labels. The default is 0 on
    /// every edge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A floor, not a size. On each edge the layout asks for the larger of this value and the room the axis's tick
    /// marks, <see cref="LabelGap"/> and widest label actually need there, so labels are never squeezed below what
    /// they need — and an edge that needs less still gets at least this much. Setting the same value on several
    /// charts aligns their plots when their labels differ in width.
    /// </para>
    /// <para>
    /// The band sits between the padding and the plot. Like <see cref="Control.Padding"/> it is a request: it
    /// outranks padding (padding is reduced first when the area is too small) and is outranked by the minimum plot
    /// size. This value itself is never changed by the control.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown by the property system if any edge is negative, NaN or infinite.
    /// </exception>
    public Thickness MinimumAxisSpace
    {
        get => (Thickness)GetValue(MinimumAxisSpaceProperty);
        set => SetValue(MinimumAxisSpaceProperty, value);
    }

    /// <summary>
    /// The axis thickness the ticks and labels need, per edge: tick length plus label gap plus the outward reach of the
    /// widest rotated label on that edge.
    /// </summary>
    /// <remarks>
    /// Internal layout diagnostics for internal validation. The layout asks for the larger of this and
    /// <see cref="MinimumAxisSpace"/> per edge. Computable before layout: label text depends on the tick values, and
    /// tick values on the range and the target count, never on the plot size.
    /// </remarks>
    internal Thickness RequiredAxisSpace => (Thickness)GetValue(RequiredAxisSpaceProperty);

    /// <summary>
    /// Gets or sets the angle the X axis's tick labels are drawn at. The default is <see cref="LabelRotation.Degree0"/>.
    /// </summary>
    /// <remarks>
    /// Chosen by the caller only: the control never rotates labels on its own because they are crowded or the chart is
    /// narrow. A rotation changes the room the labels need, so it can move the plot.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is not a defined member.</exception>
    public LabelRotation XAxisLabelRotation
    {
        get => (LabelRotation)GetValue(XAxisLabelRotationProperty);
        set => SetValue(XAxisLabelRotationProperty, value);
    }

    /// <summary>
    /// Gets or sets the angle the Y axis's tick labels are drawn at. The default is <see cref="LabelRotation.Degree0"/>.
    /// </summary>
    /// <remarks>As for <see cref="XAxisLabelRotation"/>.</remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is not a defined member.</exception>
    public LabelRotation YAxisLabelRotation
    {
        get => (LabelRotation)GetValue(YAxisLabelRotationProperty);
        set => SetValue(YAxisLabelRotationProperty, value);
    }

    /// <summary>
    /// Gets or sets which point along an X axis label's text is pinned to its tick. The default is
    /// <see cref="LabelAnchor.Center"/>.
    /// </summary>
    /// <remarks>
    /// The control default is <see cref="LabelAnchor.Center"/>, which is not the enum's zero value
    /// (<see cref="LabelAnchor.Start"/>).
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is not a defined member.</exception>
    public LabelAnchor XAxisLabelAnchor
    {
        get => (LabelAnchor)GetValue(XAxisLabelAnchorProperty);
        set => SetValue(XAxisLabelAnchorProperty, value);
    }

    /// <summary>
    /// Gets or sets which point along a Y axis label's text is pinned to its tick. The default is
    /// <see cref="LabelAnchor.Center"/>.
    /// </summary>
    /// <remarks>As for <see cref="XAxisLabelAnchor"/>.</remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is not a defined member.</exception>
    public LabelAnchor YAxisLabelAnchor
    {
        get => (LabelAnchor)GetValue(YAxisLabelAnchorProperty);
        set => SetValue(YAxisLabelAnchorProperty, value);
    }

    /// <summary>Gets or sets the gap between a tick's end and its label, in DIP. The default is 4.</summary>
    /// <remarks>
    /// One value for every edge and every rotation, measured along the outward normal, so it means the same thing on
    /// all four sides.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if negative or non-finite.</exception>
    public double LabelGap
    {
        get => (double)GetValue(LabelGapProperty);
        set => SetValue(LabelGapProperty, value);
    }

    /// <summary>
    /// The axis space actually granted per edge, which may be smaller than requested when the available area could not
    /// hold it.
    /// </summary>
    /// <remarks>
    /// Internal layout diagnostics for internal validation. Reduced only after <see cref="EffectivePadding"/> has
    /// reached zero: axis space outranks padding.
    /// </remarks>
    internal Thickness EffectiveAxisSpace => (Thickness)GetValue(EffectiveAxisSpaceProperty);

    /// <summary>Gets or sets the smallest X value shown: the left end of the X axis. The default is 0.</summary>
    /// <remarks>
    /// <para>
    /// The X range is <see cref="MinimumX"/> to <see cref="MaximumX"/>, set by the caller. The control never derives a
    /// range from the data: with no range set, every chart shows 0 to 100 on both axes.
    /// </para>
    /// <para>
    /// The range must satisfy minimum &lt; maximum for ticks, grid lines and data to be placed. The two endpoints are
    /// separate properties, so neither can reject an inverted range on its own; an inverted or degenerate range draws no
    /// ticks rather than throwing or guessing a scale.
    /// </para>
    /// <para>
    /// <b>Binds two-way by default.</b> Zoom and pan (<see cref="IsZoomEnabled"/>, <see cref="IsPanEnabled"/>) change the
    /// range with <see cref="DependencyObject.SetCurrentValue"/>, which keeps a binding in place: with the default mode
    /// the bound source receives the new range; with an explicit <c>Mode=OneWay</c> it does not, and the next source
    /// change wins.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is NaN or infinite.</exception>
    public double MinimumX
    {
        get => (double)GetValue(MinimumXProperty);
        set => SetValue(MinimumXProperty, value);
    }

    /// <summary>Gets or sets the largest X value shown: the right end of the X axis. The default is 100.</summary>
    /// <remarks>See <see cref="MinimumX"/>, whose rules it shares, binding included.</remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is NaN or infinite.</exception>
    public double MaximumX
    {
        get => (double)GetValue(MaximumXProperty);
        set => SetValue(MaximumXProperty, value);
    }

    /// <summary>Gets or sets the smallest Y value shown: the bottom end of the Y axis. The default is 0.</summary>
    /// <remarks>See <see cref="MinimumX"/>, whose rules it shares, binding included.</remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is NaN or infinite.</exception>
    public double MinimumY
    {
        get => (double)GetValue(MinimumYProperty);
        set => SetValue(MinimumYProperty, value);
    }

    /// <summary>Gets or sets the largest Y value shown: the top end of the Y axis. The default is 100.</summary>
    /// <remarks>See <see cref="MinimumX"/>, whose rules it shares, binding included.</remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is NaN or infinite.</exception>
    public double MaximumY
    {
        get => (double)GetValue(MaximumYProperty);
        set => SetValue(MaximumYProperty, value);
    }

    /// <summary>
    /// Gets or sets how many major ticks the X axis should aim for. The default is 6.
    /// </summary>
    /// <remarks>
    /// A target, not a quota. The interval is rounded up to a nice round number, so the count that results is normally
    /// within one or two of this. It is not derived from the control's width: a resize never changes a tick value.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is below 1 or above 1000.</exception>
    public int XAxisTargetTickCount
    {
        get => (int)GetValue(XAxisTargetTickCountProperty);
        set => SetValue(XAxisTargetTickCountProperty, value);
    }

    /// <summary>
    /// Gets or sets how many major ticks the Y axis should aim for. The default is 6.
    /// </summary>
    /// <remarks>As for <see cref="XAxisTargetTickCount"/>.</remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is below 1 or above 1000.</exception>
    public int YAxisTargetTickCount
    {
        get => (int)GetValue(YAxisTargetTickCountProperty);
        set => SetValue(YAxisTargetTickCountProperty, value);
    }

    /// <summary>
    /// Gets or sets which screen edge of the plot the X axis is drawn on. The default is
    /// <see cref="HorizontalAxisPosition.Bottom"/>.
    /// </summary>
    /// <remarks>
    /// The X axis runs horizontally, so it takes the bottom or the top edge. The reserved axis space follows: the band
    /// the labels need is on the chosen edge.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is not a defined member.</exception>
    public HorizontalAxisPosition XAxisPosition
    {
        get => (HorizontalAxisPosition)GetValue(XAxisPositionProperty);
        set => SetValue(XAxisPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets which screen edge of the plot the Y axis is drawn on. The default is
    /// <see cref="VerticalAxisPosition.Left"/>.
    /// </summary>
    /// <remarks>The Y axis runs vertically, so it takes the left or the right edge.</remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is not a defined member.</exception>
    public VerticalAxisPosition YAxisPosition
    {
        get => (VerticalAxisPosition)GetValue(YAxisPositionProperty);
        set => SetValue(YAxisPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the X axis's tick marks and labels are drawn. The default is true.
    /// </summary>
    /// <remarks>
    /// Drawing only: hiding an axis does not release the space reserved for it, so the plot neither moves nor resizes.
    /// The vertical grid lines, drawn at the X axis's ticks, are independent of it.
    /// </remarks>
    public bool IsXAxisVisible
    {
        get => (bool)GetValue(IsXAxisVisibleProperty);
        set => SetValue(IsXAxisVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the Y axis's tick marks and labels are drawn. The default is true.
    /// </summary>
    /// <remarks>As for <see cref="IsXAxisVisible"/>.</remarks>
    public bool IsYAxisVisible
    {
        get => (bool)GetValue(IsYAxisVisibleProperty);
        set => SetValue(IsYAxisVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the horizontal major grid lines are drawn. The default is true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Horizontal</b> names the screen direction the lines run, not the axis they come from: these are
    /// the lines drawn across the plot at each <em>Y</em> axis major tick. Turning them off
    /// leaves the Y axis's ticks and labels exactly as they were.
    /// </para>
    /// <para>
    /// Drawing only. The grid takes no part in the layout, so this never moves or resizes the plot,
    /// and it never changes a tick: the lines are drawn from the tick sequence the axis already has.
    /// </para>
    /// <para>
    /// Independent of <see cref="IsYAxisVisible"/>. The grid depends on the Y axis's
    /// tick <em>data</em>, which exists whether or not the axis is drawn, and not on the axis's
    /// visual; a chart with a hidden axis and a visible grid is a supported combination.
    /// </para>
    /// </remarks>
    public bool IsHorizontalMajorGridVisible
    {
        get => (bool)GetValue(IsHorizontalMajorGridVisibleProperty);
        set => SetValue(IsHorizontalMajorGridVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the vertical major grid lines are drawn. The default is true.
    /// </summary>
    /// <remarks>
    /// <b>Vertical</b> names the screen direction the lines run: these are the lines drawn down the plot at
    /// each <em>X</em> axis major tick. Drawing only, and independent of
    /// <see cref="IsXAxisVisible"/>, for the reasons given on
    /// <see cref="IsHorizontalMajorGridVisible"/>.
    /// </remarks>
    public bool IsVerticalMajorGridVisible
    {
        get => (bool)GetValue(IsVerticalMajorGridVisibleProperty);
        set => SetValue(IsVerticalMajorGridVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush major grid lines are stroked with. The default is a solid <c>#FF34343E</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A null brush draws no grid. The default — set by the default style and, with the same colour, as the property's
    /// own default, so a style that does not base itself on the default one still draws a grid — is a frozen brush the
    /// library owns.
    /// </para>
    /// <para>
    /// <b>A brush set here is the caller's and is never frozen or modified.</b> A mutable brush may be changed after it
    /// is set: the control draws with a frozen snapshot and takes a new one when the brush changes.
    /// </para>
    /// </remarks>
    public Brush? MajorGridBrush
    {
        get => (Brush?)GetValue(MajorGridBrushProperty);
        set => SetValue(MajorGridBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke thickness of a major grid line, in DIP. The default is 1.
    /// </summary>
    /// <remarks>
    /// Styling only: it changes how wide a line is painted, never where the line is. Zero is
    /// accepted and draws nothing. The stroke is centred on the line and clipped to the plot, so a
    /// thick line at the plot edge is cut at the edge rather than painted into the axis band.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if negative or non-finite.</exception>
    public double MajorGridThickness
    {
        get => (double)GetValue(MajorGridThicknessProperty);
        set => SetValue(MajorGridThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the horizontal origin line — the line at y = 0 — is drawn. The default is
    /// true.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Horizontal</b> names the screen direction the line runs: it marks the value 0 on the
    /// <em>Y</em> axis, and runs across the plot from the left edge to the right.
    /// </para>
    /// <para>
    /// Drawn only when the Y range includes 0 — <see cref="MinimumY"/> ≤ 0 ≤
    /// <see cref="MaximumY"/>, endpoints included — whatever this property says. A range that does
    /// not include 0 has no origin to mark, and the line is not moved to the nearest edge. A range that
    /// ends at 0 puts it exactly on the plot edge. Whether a major tick happens to fall at 0 plays no part.
    /// </para>
    /// <para>
    /// Drawing only: it never moves or resizes the plot, and never changes a tick, a label or the grid.
    /// </para>
    /// </remarks>
    public bool IsHorizontalOriginLineVisible
    {
        get => (bool)GetValue(IsHorizontalOriginLineVisibleProperty);
        set => SetValue(IsHorizontalOriginLineVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the vertical origin line — the line at x = 0 — is drawn. The default is true.
    /// </summary>
    /// <remarks>
    /// <b>Vertical</b> names the screen direction the line runs: it marks the value 0 on the <em>X</em>
    /// axis, and runs down the plot from the top edge to the bottom. Drawn only when
    /// <see cref="MinimumX"/> ≤ 0 ≤ <see cref="MaximumX"/>, and drawing only, for the
    /// reasons given on <see cref="IsHorizontalOriginLineVisible"/>.
    /// </remarks>
    public bool IsVerticalOriginLineVisible
    {
        get => (bool)GetValue(IsVerticalOriginLineVisibleProperty);
        set => SetValue(IsVerticalOriginLineVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush both origin lines are stroked with. The default is a solid <c>#FF6E6E7C</c>.
    /// </summary>
    /// <remarks>
    /// A null brush draws no origin line. The default is a frozen brush the library owns, set both by the default style
    /// and as the property's own default. A brush set here is the caller's and is never frozen or modified: the control
    /// draws with a frozen snapshot and takes a new one when a mutable brush changes.
    /// </remarks>
    public Brush? OriginLineBrush
    {
        get => (Brush?)GetValue(OriginLineBrushProperty);
        set => SetValue(OriginLineBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke thickness of both origin lines, in DIP. The default is 1.5.
    /// </summary>
    /// <remarks>
    /// Styling only: it changes how wide a line is painted, never where it is. Zero is accepted and draws
    /// nothing. The stroke is clipped to the plot, so an origin on a plot edge is cut at the edge rather
    /// than painted into the axis band.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if negative or non-finite.</exception>
    public double OriginLineThickness
    {
        get => (double)GetValue(OriginLineThicknessProperty);
        set => SetValue(OriginLineThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush reference lines are stroked with when they do not name one of their own.
    /// The default is a solid <c>#FFB08A4E</c>.
    /// </summary>
    /// <remarks>
    /// The fallback for every <see cref="ChartReferenceLine"/> whose <see cref="ChartReferenceLine.Stroke"/>
    /// is null, so the lines follow the control's style without each one naming a colour. Null here, with
    /// no stroke on the line either, draws that line nothing. The default is a frozen brush the library owns, set both
    /// by the default style and as the property's own default. A brush set here is the caller's and is never frozen or
    /// modified.
    /// </remarks>
    public Brush? ReferenceLineBrush
    {
        get => (Brush?)GetValue(ReferenceLineBrushProperty);
        set => SetValue(ReferenceLineBrushProperty, value);
    }

    /// <summary>
    /// Gets this control's reference lines: lines drawn across the plot at values the caller chooses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each control has its own collection, created with it and never shared with another instance. Add,
    /// remove, clear or change any <see cref="ChartReferenceLine"/> in it — its value, axis, visibility,
    /// stroke or stroke thickness — and the lines are redrawn. The control never freezes or modifies a line it is given. Nothing in the collection takes any part in the
    /// layout, so no change to it moves the plot or invalidates measure or arrange.
    /// </para>
    /// <para>
    /// Lines are kept exactly as given: a line whose value lies outside its axis range is kept and not
    /// drawn, two lines at the same value are two lines, and a line at 0 coexists with the origin line
    /// (which is drawn over it). Drawing order is collection order.
    /// </para>
    /// <para>
    /// Read-only: the collection is replaced by nobody, only filled. In XAML the items are written as
    /// children of the property element.
    /// </para>
    /// </remarks>
    public FreezableCollection<ChartReferenceLine> ReferenceLines
        => (FreezableCollection<ChartReferenceLine>)GetValue(ReferenceLinesProperty);

    /// <summary>
    /// Gets or sets the brush series are stroked with when they do not name one of their own. The default
    /// is a solid <c>#FF4FA3E8</c>.
    /// </summary>
    /// <remarks>
    /// The fallback for every <see cref="LineSeries"/> whose <see cref="LineSeries.Stroke"/> is null, so a
    /// series follows the control's style without naming a colour. Null here, with no stroke on the series
    /// either, draws that series nothing. The default is a frozen brush the library owns, set both by the default style
    /// and as the property's own default. A brush set here is the caller's and is never frozen or modified: the control
    /// draws with a frozen snapshot and takes a new one when a mutable brush changes.
    /// </remarks>
    public Brush? SeriesStroke
    {
        get => (Brush?)GetValue(SeriesStrokeProperty);
        set => SetValue(SeriesStrokeProperty, value);
    }

    /// <summary>
    /// Gets this control's series: the data drawn in the plot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each control has its own collection, created with it and never shared with another instance. Add,
    /// remove or clear series, or change any <see cref="LineSeries"/> in it — set new points, show or hide it,
    /// change its stroke or thickness, or change a mutable brush it holds — and the series are redrawn.
    /// Nothing in the collection takes any part in the layout, so no change to it moves the plot or
    /// invalidates measure or arrange.
    /// </para>
    /// <para>
    /// Every series is placed against this control's <see cref="MinimumX"/> to
    /// <see cref="MaximumX"/> and <see cref="MinimumY"/> to <see cref="MaximumY"/>:
    /// there is no axis per series. Series are kept exactly as given — two identical series are two series —
    /// and drawn in collection order, so a series added later is drawn over the ones before it.
    /// </para>
    /// <para>
    /// A series' points are a snapshot: setting <see cref="LineSeries.Points"/> replaces them and redraws; changing the
    /// content of a list already set is not noticed. <see cref="LineSeries.Append"/> adds one point,
    /// <see cref="LineSeries.AppendRange"/> adds a batch and <see cref="LineSeries.ClearPoints"/> removes them all, each
    /// with one redraw. See <see cref="LineSeries.Points"/>.
    /// </para>
    /// <para>
    /// The control never freezes or modifies a series it is given, the caller's point lists, or the caller's brushes.
    /// </para>
    /// <para>
    /// Read-only: the collection is replaced by nobody, only filled.
    /// </para>
    /// </remarks>
    public FreezableCollection<LineSeries> Series
        => (FreezableCollection<LineSeries>)GetValue(SeriesProperty);

    /// <summary>
    /// Gets or sets the absolute minimum width, in device-independent pixels, to keep for
    /// the plot area. The default is 0, meaning no absolute floor.
    /// </summary>
    /// <remarks>
    /// Combined with <see cref="MinimumPlotRatio"/> by taking whichever yields the larger
    /// minimum. If the result exceeds the available width it is clamped: the control does
    /// not grow to satisfy it.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown by the property system if the value is negative, NaN or infinite.
    /// </exception>
    public double MinimumPlotWidth
    {
        get => (double)GetValue(MinimumPlotWidthProperty);
        set => SetValue(MinimumPlotWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the absolute minimum height, in device-independent pixels, to keep
    /// for the plot area. The default is 0, meaning no absolute floor.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown by the property system if the value is negative, NaN or infinite.
    /// </exception>
    public double MinimumPlotHeight
    {
        get => (double)GetValue(MinimumPlotHeightProperty);
        set => SetValue(MinimumPlotHeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum fraction of the available area to keep for the plot, from
    /// 0 to 1 inclusive. The default is 0.80.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At the default, a 1000 DIP wide available area keeps at least 800 DIP for the plot. Combined with
    /// <see cref="MinimumPlotWidth"/> and <see cref="MinimumPlotHeight"/> by taking whichever yields the larger minimum.
    /// </para>
    /// <para>
    /// The minimum plot outranks axis space and padding: when the area is too small, padding is reduced first, then the
    /// axis bands (so labels may lose room on a very small chart), and the minimum plot is kept. 0 disables the ratio.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown by the property system if the value is outside 0 to 1, NaN or infinite.
    /// </exception>
    public double MinimumPlotRatio
    {
        get => (double)GetValue(MinimumPlotRatioProperty);
        set => SetValue(MinimumPlotRatioProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether content drawn in the plot area is clipped
    /// to <see cref="PlotBounds"/>. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This clips plot-area content only: the series, and whatever a template puts into
    /// <see cref="ContentHostPartName"/>. It is deliberately not a whole-chart clip: axes, labels and tooltips
    /// live outside the plot area and are never cut off by it, and the grid, origin and
    /// reference lines are always held to the plot whatever it says. Clipping is a drawing
    /// constraint and takes no part in the layout calculation, so toggling it never moves or
    /// resizes the plot.
    /// </para>
    /// <para>
    /// When it is false, nothing in the control clips the series: a line running off the plot is
    /// drawn over the axis bands — beneath the ticks and labels, which are above it — and past
    /// the control's edge as far as the host allows. The geometry is identical either way; only
    /// the clip differs.
    /// </para>
    /// </remarks>
    public bool ClipToPlotArea
    {
        get => (bool)GetValue(ClipToPlotAreaProperty);
        set => SetValue(ClipToPlotAreaProperty, value);
    }

    /// <summary>Gets or sets whether the lens is shown. The default is false.</summary>
    /// <remarks>
    /// <para>
    /// The lens shows the X range <see cref="LensMinimumX"/>..<see cref="LensMaximumX"/> magnified across part of the
    /// plot's width — <see cref="LensWidth"/> wide, at <see cref="LensHorizontalPosition"/> — at the plot's full height.
    /// Inside it the chart is drawn again through the lens's X mapping and the main chart's own Y mapping: the grid, the
    /// reference and origin lines, every visible series, and X tick marks and labels for the lens's own range, formatted,
    /// rotated and anchored as the X axis's labels are. Y is not magnified, so the lens has no Y axis of its own:
    /// the main Y axis labels it.
    /// </para>
    /// <para>
    /// Drawing only: showing, hiding, moving or resizing the lens never moves the plot, the axis bands or anything the
    /// main chart draws. The lens is drawn into the template part <see cref="LensHostPartName"/>; a template without it
    /// draws no lens.
    /// </para>
    /// <para>
    /// Like every property of the control it is set on the control's thread; from another thread it throws
    /// <see cref="InvalidOperationException"/>. It may be bound, styled or animated as any dependency property.
    /// </para>
    /// </remarks>
    public bool IsLensVisible
    {
        get => (bool)GetValue(IsLensVisibleProperty);
        set => SetValue(IsLensVisibleProperty, value);
    }

    /// <summary>Gets or sets the smallest X value the lens shows. The default is 40.</summary>
    /// <remarks>
    /// <para>
    /// With <see cref="LensMaximumX"/>, the lens's source: the X range shown magnified. It must be finite; minimum below
    /// maximum is required for a lens to be drawn, but the two are separate properties and neither can refuse the other's
    /// value — an inverted or degenerate source draws no lens, as an inverted range draws no tick.
    /// </para>
    /// <para>
    /// A request. The source drawn is the part of it inside <see cref="MinimumX"/>..<see cref="MaximumX"/>:
    /// an intersection, never a clamp, so the lens shows exactly the values both ranges hold, stretched across the whole
    /// lens. A source wholly outside the main range draws no lens. The requested values are kept, so a source that leaves
    /// the main range as it scrolls is shown again, in full, when the range returns to it.
    /// </para>
    /// <para>
    /// The defaults 40 and 60 are the middle fifth of the default 0 to 100 range; with another range they may lie
    /// outside it, and the lens then draws nothing until they are set.
    /// </para>
    /// <para>
    /// <b>Binds two-way by default</b>, as the lens gestures (<see cref="IsLensInteractionEnabled"/>) change it with
    /// <see cref="DependencyObject.SetCurrentValue"/>; an explicit <c>Mode=OneWay</c> binding stays one-way.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is NaN or infinite.</exception>
    public double LensMinimumX
    {
        get => (double)GetValue(LensMinimumXProperty);
        set => SetValue(LensMinimumXProperty, value);
    }

    /// <summary>Gets or sets the largest X value the lens shows. The default is 60.</summary>
    /// <remarks>
    /// The other end of the lens's source; see <see cref="LensMinimumX"/>, whose rules it shares, binding included.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if the value is NaN or infinite.</exception>
    public double LensMaximumX
    {
        get => (double)GetValue(LensMaximumXProperty);
        set => SetValue(LensMaximumXProperty, value);
    }

    /// <summary>Gets or sets the lens's width, in device-independent pixels. The default is 200.</summary>
    /// <remarks>
    /// A request: the lens is drawn <c>min(LensWidth, plot width)</c> wide, and this value is not changed when the plot
    /// is narrower, so the lens returns to the requested width when the plot grows again. Zero draws no lens. Binds
    /// two-way by default, as the other lens state does.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if negative, NaN or infinite.</exception>
    public double LensWidth
    {
        get => (double)GetValue(LensWidthProperty);
        set => SetValue(LensWidthProperty, value);
    }

    /// <summary>Gets or sets where the lens stands along the plot, from 0 to 1. The default is 0.5, centred.</summary>
    /// <remarks>
    /// A fraction of the lens's travel: 0 puts its left edge on the plot's left edge, 1 its right edge on the plot's right
    /// edge. The lens therefore stays inside the plot at every value, and keeps its relative place as the plot resizes.
    /// It places the lens on the screen only; what the lens shows is <see cref="LensMinimumX"/>..<see cref="LensMaximumX"/>.
    /// Binds two-way by default: dragging the lens changes it with <see cref="DependencyObject.SetCurrentValue"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if outside 0 to 1, NaN or infinite.</exception>
    public double LensHorizontalPosition
    {
        get => (double)GetValue(LensHorizontalPositionProperty);
        set => SetValue(LensHorizontalPositionProperty, value);
    }

    /// <summary>The lens state as requested, read from its five properties.</summary>
    internal LensState LensState => new(IsLensVisible, LensMinimumX, LensMaximumX, LensHorizontalPosition, LensWidth);

    /// <summary>Re-places the lens when anything of its state changes; nothing else is marked.</summary>
    /// <remarks>
    /// The lens's horizontal mapping is dropped and the lens layer marked: no main layer, and no layout, is drawn from the
    /// lens state, so none is marked and no arrange is asked for.
    /// </remarks>
    private static void OnLensStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        control._lensPart = null;
        control.Recompose(ChartLayers.Lens);

        // The pointer may now be over the lens, or no longer.
        control.RefreshInteraction();
    }

    /// <summary>
    /// The content host part of the applied template, or <see langword="null"/> if no template has been applied or the
    /// part is absent: a replacement template need not supply it.
    /// </summary>
    private FrameworkElement? ContentHost { get; set; }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        ContentHost = GetTemplateChild(ContentHostPartName) as FrameworkElement;

        // One drawing element serves every tick, created here rather than declared in the
        // template so the template never has to name a type that is not public.
        _tickLayer = null;
        _labelLayer = null;
        _gridLayer = null;
        _referenceLayer = null;
        _seriesLayer = null;
        _lensLayer = null;
        _interactionLayer = null;

        // The grid has a host of its own rather than a place in the axis host, because z-order is
        // template order: the grid must sit beneath the plot content, and the axis host sits above it.
        if (GetTemplateChild(GridHostPartName) is Panel gridHost)
        {
            _gridLayer = new MajorGridLayer(BuildGrid);
            gridHost.Children.Add(_gridLayer);
        }

        // Origin and reference lines likewise, one host above the grid's: they are drawn over the grid
        // lines they may coincide with, and beneath the plot content and the axes.
        if (GetTemplateChild(ReferenceLineHostPartName) is Panel referenceHost)
        {
            _referenceLayer = new ReferenceLineLayer(BuildReferenceLines);
            referenceHost.Children.Add(_referenceLayer);
        }

        // Series likewise, one host above the reference lines' and below the axes'. One element for every
        // series and every point.
        if (GetTemplateChild(SeriesHostPartName) is Panel seriesHost)
        {
            _seriesLayer = new SeriesLayer(BuildSeries);
            seriesHost.Children.Add(_seriesLayer);
        }

        // The lens, one host above the plot content and below the axes: one element, whatever the lens
        // shows, present whether or not the lens is, so showing it adds nothing to the visual tree.
        if (GetTemplateChild(LensHostPartName) is Panel lensHost)
        {
            _lensLayer = new LensLayer(BuildLens);
            lensHost.Children.Add(_lensLayer);
        }

        // The crosshair and the tooltip, one host above the lens and below the axes: one element for both,
        // present whether or not either is shown, so showing them adds nothing to the visual tree.
        if (GetTemplateChild(InteractionHostPartName) is Panel interactionHost)
        {
            _interactionLayer = new InteractionLayer(BuildInteraction);
            interactionHost.Children.Add(_interactionLayer);
        }

        if (GetTemplateChild(AxisHostPartName) is Panel axisHost)
        {
            _tickLayer = new AxisTickLayer(BuildTicks);
            axisHost.Children.Add(_tickLayer);

            // Labels after marks, so a label is never painted under its own tick. Two layers in
            // total, whatever the tick count: neither grows a child per tick or per label.
            _labelLayer = new AxisLabelLayer(BuildLabels);
            axisHost.Children.Add(_labelLayer);
        }

        ApplyPlotClip();
        InvalidateLabelCache();
        DropLensContent();
        DropOverlayStyle();
        _interactionShown = true;
        Invalidate(ChartLayers.Geometry);
    }

    /// <summary>
    /// Marks the given layers stale with their geometry, and asks for an arrange when the layout is among them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dirty model's entry point for a change that moves what the layers place: a range, the tick
    /// values, the plot, an axis's position or visibility, a grid's visibility. Every layer in
    /// <paramref name="layers"/> is composed again at its next render, and its cached geometry is
    /// dropped first, so the composition places it again: every grid line, every reference and origin line, every
    /// series, every tick mark and label placement of those layers. Their styles — the pens, the measured text — are
    /// not drawn from any of these inputs and are kept.
    /// </para>
    /// <para>
    /// A change that moves nothing goes through <see cref="Recompose"/> instead, after dropping the one cached part
    /// it does reach.
    /// </para>
    /// <para>
    /// <b>One axis or both</b>. Only the half of each layer drawn from <paramref name="axes"/> is dropped:
    /// a horizontal range drops the vertical grid lines, the X reference lines, the vertical origin, the bottom or top
    /// axis's ticks and label placements — and keeps the horizontal grid lines, the Y reference lines, the horizontal
    /// origin and the left or right axis as they were, because nothing they are drawn from changed. The layers are
    /// marked exactly as before: the dirty model's layers are unchanged, and only what a composition makes again is
    /// narrower. A series is drawn from both axes, so either drops it.
    /// </para>
    /// </remarks>
    private void Invalidate(ChartLayers layers, ValueAxes axes = ValueAxes.Both)
    {
        var x = (axes & ValueAxes.Horizontal) != 0;
        var y = (axes & ValueAxes.Vertical) != 0;

        if ((layers & ChartLayers.Grid) != 0)
        {
            // Vertical lines stand at the horizontal axis's ticks, horizontal lines at the vertical axis's.
            if (x)
            {
                _verticalGridLines = null;
            }

            if (y)
            {
                _horizontalGridLines = null;
            }
        }

        if ((layers & ChartLayers.Reference) != 0)
        {
            // Every line's entry is kept — it carries what its pen was made from — but none of the placed geometry of
            // the lines on that axis is valid in the new epoch. x = 0 is a vertical line, from the horizontal range.
            if (x)
            {
                _referenceEpochX++;
                _verticalOrigin = null;
            }

            if (y)
            {
                _referenceEpochY++;
                _horizontalOrigin = null;
            }
        }

        if ((layers & ChartLayers.Series) != 0 && (x || y))
        {
            _seriesEpoch++;
        }

        if ((layers & ChartLayers.Axis) != 0)
        {
            if (x)
            {
                _horizontalTickMarks = null;
                _horizontalLabelPlacements = null;
            }

            if (y)
            {
                _verticalTickMarks = null;
                _verticalLabelPlacements = null;
            }
        }

        // The lens's rectangle and source are placed from the plot and the main horizontal range: either
        // drops them, and its X content with them. A vertical range reaches only the lens's Y mapping, which is the main
        // chart's own: the lens keeps its placement, and what it draws by Y — grid, references, origin, series — moves to
        // a new Y epoch.
        if ((layers & ChartLayers.Lens) != 0)
        {
            if (x)
            {
                _lensPart = null;
            }

            if (y)
            {
                _lensEpochY++;
            }
        }

        Recompose(layers);

        // The crosshair and the tooltip are placed against the plot and read the ranges, as the axis and the
        // lens are: a change that moves either reaches the overlay too. A grid's visibility alone does not.
        if ((layers & (ChartLayers.Axis | ChartLayers.Lens)) != 0)
        {
            RefreshInteraction();
        }
    }

    /// <summary>The value axis a range or target property belongs to.</summary>
    private static ValueAxes AxisOf(DependencyProperty property)
        => property == MinimumXProperty || property == MaximumXProperty || property == XAxisTargetTickCountProperty
            ? ValueAxes.Horizontal
            : ValueAxes.Vertical;

    /// <summary>
    /// Marks the given layers stale, and asks for an arrange when the layout is among them, keeping every cache.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one place a layer is marked: every change the control hears of arrives here, through
    /// <see cref="Invalidate"/> or directly, with the layers drawn from what changed, and nothing else is marked.
    /// Constant time: a layer is only flagged and invalidated, and is composed when WPF renders it — once, however
    /// many changes came before (<see cref="DeferredLayer{TFrame}"/>).
    /// </para>
    /// <para>
    /// <b>Composed, not rebuilt</b>. Called directly, nothing cached is dropped here: the caller has
    /// already dropped the one part the change reaches — a pen for a brush, the placements for a rotation, nothing at
    /// all for a reference line or a series, whose own generations tell the composition which of them changed — and
    /// the composition reuses the rest. A grid brush therefore re-strokes the lines already placed.
    /// </para>
    /// <para>
    /// <see cref="ChartLayers.Layout"/> becomes <see cref="UIElement.InvalidateArrange"/>. Which layers the arrange
    /// then marks is decided by the arrange, from what it computed; see <see cref="ArrangeOverride"/>.
    /// </para>
    /// </remarks>
    private void Recompose(ChartLayers layers)
    {
        if ((layers & ChartLayers.Grid) != 0 && _gridLayer is { } grid)
        {
            _counters.GridInvalidations++;
            grid.Invalidate();
        }

        if ((layers & ChartLayers.Reference) != 0 && _referenceLayer is { } reference)
        {
            _counters.ReferenceInvalidations++;
            reference.Invalidate();
        }

        if ((layers & ChartLayers.Series) != 0 && _seriesLayer is { } series)
        {
            _counters.SeriesInvalidations++;
            series.Invalidate();
        }

        if ((layers & ChartLayers.Axis) != 0 && (_tickLayer is not null || _labelLayer is not null))
        {
            _counters.AxisInvalidations++;
            _tickLayer?.Invalidate();
            _labelLayer?.Invalidate();
        }

        // A hidden lens that drew nothing last is not composed for a change it would not draw. It keeps no series or
        // reference entry, and the rest it draws from is kept by generation and epoch, so it shows the latest state when
        // it is shown again — which composes it, as does the change that hides it.
        if ((layers & ChartLayers.Lens) != 0 && _lensLayer is { } lens && (IsLensVisible || _lensShown))
        {
            _lensCounters.Invalidations++;
            lens.Invalidate();
        }

        if ((layers & ChartLayers.Layout) != 0)
        {
            _counters.LayoutRequests++;
            InvalidateArrange();
        }
    }

    /// <summary>Holds back every layer's build while the template is arranged; see <see cref="DeferredLayer{TFrame}.Hold"/>.</summary>
    private void HoldLayers()
    {
        _gridLayer?.Hold();
        _referenceLayer?.Hold();
        _seriesLayer?.Hold();
        _lensLayer?.Hold();
        _interactionLayer?.Hold();
        _tickLayer?.Hold();
        _labelLayer?.Hold();
    }

    /// <summary>Ends <see cref="HoldLayers"/>.</summary>
    private void ReleaseLayers()
    {
        _gridLayer?.Release();
        _referenceLayer?.Release();
        _seriesLayer?.Release();
        _lensLayer?.Release();
        _interactionLayer?.Release();
        _tickLayer?.Release();
        _labelLayer?.Release();
    }

    /// <summary>
    /// How many times each layer was invalidated and built, and how many layout passes the control ran.
    /// </summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, which subtracts two readings to prove what one change
    /// cost — that a series append built the series and nothing else, say. Internal: it is not a public API.
    /// </remarks>
    internal RenderCounters Counters => _counters;

    /// <summary>
    /// How often each layer's cached geometry and style were reused, and how often made again.
    /// </summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, which subtracts two readings to prove what
    /// one change cost inside the compositions <see cref="Counters"/> counts — that a grid brush change made one pen
    /// and placed no line, say. Internal: it is not a public API.
    /// </remarks>
    internal CacheCounters CacheCounters => _cacheCounters;

    /// <summary>How many times the lens was invalidated and composed, and its mapping placed or reused.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, kept out of <see cref="Counters"/> and
    /// <see cref="CacheCounters"/> so no main-layer reading changes. Internal: it is not a public API.
    /// </remarks>
    internal LensCounters LensCounters => _lensCounters;

    /// <summary>How many points the series builds walked, and which gestures changed the chart.</summary>
    /// <remarks>
    /// Read-only internal instrumentation, kept out of every other
    /// counter so no other reading changes. Internal: it is not a public API.
    /// </remarks>
    internal ProcessingCounters ProcessingCounters => _processingCounters;

    /// <summary>How many cache entries the control holds: one per reference line and one per series, at most.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation: the cache-growth monitor holds these to the
    /// collections' sizes, so a cache that kept the entries of removed items, or one entry per update, is caught.
    /// </remarks>
    internal (int References, int Series, int ReferencePens, int SeriesPens) CacheEntries
        => (_referenceEntries.Count, _seriesEntries.Count, _referencePens.Count, _seriesPens.Count);

    /// <summary>
    /// How many sub-part geometry caches the control holds per value axis: grid lines, origin, tick marks, label
    /// placements, and measured label text — at most one of each per axis.
    /// </summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation: the memory monitor holds each to one, so a
    /// cache that kept a second generation of one axis's geometry — one per range visited, say — is caught.
    /// </remarks>
    internal (int X, int Y) AxisCacheParts
        => (Held(_verticalGridLines) + Held(_verticalOrigin) + Held(_horizontalTickMarks) + Held(_horizontalLabelPlacements) + (_horizontalLabelTextsDirty ? 0 : 1),
            Held(_horizontalGridLines) + Held(_horizontalOrigin) + Held(_verticalTickMarks) + Held(_verticalLabelPlacements) + (_verticalLabelTextsDirty ? 0 : 1));

    private static int Held(object? part) => part is null ? 0 : 1;

    /// <summary>
    /// Drops every cache the control keeps and marks every layer and the layout, so the next render builds
    /// everything from the current state, as a chart with no history would.
    /// </summary>
    /// <remarks>
    /// Validation only: the stale-cache monitor reads what the layers draw, calls this, renders, and
    /// reads again. A cache that was stale — reused although something it is drawn from had changed — is exactly a
    /// difference between the two readings. Never called by the control. Internal: it is not a public API.
    /// </remarks>
    internal void DropCachesForValidation()
    {
        _gridPen = null;
        _originPenValid = false;
        _originPen = null;
        _referencePens = [];
        _referenceEntries = [];
        _seriesPens = [];
        _seriesEntries = [];
        _tickPen = null;
        InvalidateLabelCache();

        // And the label texts a rebuild may reuse: the rebuild after this shapes every label afresh.
        _horizontalLabelStyle = null;
        _verticalLabelStyle = null;
        _lensLabelStyle = null;
        DropLensContent();
        DropOverlayStyle();
        Invalidate(ChartLayers.Geometry | ChartLayers.Layout);
    }

    /// <summary>
    /// Drops the geometry of one value axis's half of every layer — its grid lines, reference lines and origin, tick
    /// marks, label placements, measured text and reach — and marks the layers, as a change to that axis's range would.
    /// </summary>
    /// <remarks>
    /// Validation only, as <see cref="DropCachesForValidation()"/> is: internal validation's unnecessary-build
    /// detector hands the control this <em>damage input</em> beside a change to the other axis, and the monitor must
    /// report the builds it causes as builds the change did not need. It changes no production path. Internal: it is
    /// not a public API.
    /// </remarks>
    internal void DropCachesForValidation(ValueAxes axes)
    {
        InvalidateLabelCache(axes);

        // With the label texts that axis's rebuild may reuse.
        if ((axes & ValueAxes.Horizontal) != 0)
        {
            _horizontalLabelStyle = null;
            _lensLabelStyle = null;
        }

        if ((axes & ValueAxes.Vertical) != 0)
        {
            _verticalLabelStyle = null;
        }

        // The lens's labels are the horizontal axis's.
        if ((axes & ValueAxes.Horizontal) != 0)
        {
            InvalidateLensLabels();
        }

        Invalidate(ChartLayers.Geometry | ChartLayers.Layout, axes);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Unchanged in what it does: the base measure of the template. Overridden only so internal validation can
    /// count measure passes; see <see cref="Counters"/>.
    /// </remarks>
    protected override Size MeasureOverride(Size constraint)
    {
        _counters.Measures++;

        return base.MeasureOverride(constraint);
    }

    /// <summary>
    /// Arranges the template, computes the plot area (<see cref="PlotBounds"/>) and arranges the content host part onto it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The base implementation arranges the template's visual tree. The plot rectangle is then computed from the area
    /// inside <see cref="Control.BorderThickness"/>, and <see cref="ContentHostPartName"/>, if the template has it, is
    /// arranged onto the plot only, never over the axis bands.
    /// </para>
    /// <para>
    /// The template's border applies <see cref="Control.BorderThickness"/> but not <see cref="Control.Padding"/>, so
    /// padding is applied here exactly once; a template that also applied it would subtract it twice.
    /// <see cref="MinimumAxisSpace"/> is applied in the same single place, for the same reason.
    /// </para>
    /// <para>
    /// Every layer is placed against the plot rectangle, and the tick marks and labels against the axis bands too. An
    /// arrange that computes a different plot redraws every layer; one that moves only the axis bands redraws the axes;
    /// one that computes exactly what the layers were last placed against redraws nothing.
    /// </para>
    /// </remarks>
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        Size arranged;

        _counters.Arranges++;

        // Every layer is built once, after this pass, against the plot it computes: while the template is
        // arranged no layer builds against the plot of the last pass. See DeferredLayer.Hold.
        HoldLayers();

        try
        {
            arranged = base.ArrangeOverride(arrangeBounds);
        }
        finally
        {
            ReleaseLayers();
        }

        // The area inside the border. Derived from the border thickness rather than read
        // back from the host, so the calculation does not depend on how the template
        // chose to stretch or align the part.
        var border = BorderThickness;
        var available = new Size(
            Math.Max(0, arranged.Width - border.Left - border.Right),
            Math.Max(0, arranged.Height - border.Top - border.Bottom));

        // Resolved before the layout runs, never after. Label text follows from the tick
        // values, and tick values follow from the range and the target count — none of it from
        // the plot rectangle — so what the labels need is known without first knowing what they
        // get. That is what keeps this a single pass instead of a settling loop.
        var required = ComputeRequiredAxisSpace();

        SetValue(RequiredAxisSpacePropertyKey, required);

        var result = Calculate(available, RequestedAxisSpace(required));
        UpdateLayoutResults(result);

        if (ContentHost is { } host)
        {
            var plotSize = new Size(result.PlotBounds.Width, result.PlotBounds.Height);

            // Re-measure at the plot size before arranging. An element is never arranged
            // larger than it was measured, so without this the host would collapse to the
            // desired size the base pass gave it.
            host.Measure(plotSize);

            // Offset by the border so the coordinates land in this control's space.
            host.Arrange(new Rect(
                border.Left + result.PlotBounds.X,
                border.Top + result.PlotBounds.Y,
                plotSize.Width,
                plotSize.Height));
        }

        ApplyPlotClip();

        // What every layer is placed against, in the control's own space. Compared with what the layers were last
        // placed against before it replaces it: only a layer whose placement moved is marked here.
        var plot = new Rect(
            border.Left + result.PlotBounds.X,
            border.Top + result.PlotBounds.Y,
            result.PlotBounds.Width,
            result.PlotBounds.Height);

        // The public PlotBounds is this very rectangle: the space every layer, the content host and the pointer use, so
        // an overlay a host places at it lands on the plot whatever the border. The calculator's own, inside-border result
        // is kept apart for internal validation and draws nothing.
        SetValue(PlotBoundsPropertyKey, plot);
        var axisSpace = result.EffectiveAxisSpace;
        var moved = plot != _tickPlotBounds
            ? ChartLayers.Geometry
            : axisSpace != _tickAxisSpace ? ChartLayers.Axis : ChartLayers.None;

        // A moved plot reaches both axes' halves of every layer. Bands that moved around an unmoved plot reach only the
        // axis drawn into them: the horizontal axis's ticks and labels stand in the bottom or top band, the
        // vertical axis's in the left or right one.
        var axes = moved == ChartLayers.Geometry
            ? ValueAxes.Both
            : (axisSpace.Top != _tickAxisSpace.Top || axisSpace.Bottom != _tickAxisSpace.Bottom ? ValueAxes.Horizontal : ValueAxes.None)
                | (axisSpace.Left != _tickAxisSpace.Left || axisSpace.Right != _tickAxisSpace.Right ? ValueAxes.Vertical : ValueAxes.None);

        _tickPlotBounds = plot;
        _tickAxisSpace = axisSpace;

        if (moved != ChartLayers.None)
        {
            _counters.LayoutChanges++;
            Invalidate(moved, axes);
        }

        return arranged;
    }

    /// <summary>
    /// The axis space the layout is asked for: the caller's request, widened to fit the labels.
    /// </summary>
    /// <param name="required">What ticks and labels need, per edge.</param>
    /// <remarks>
    /// <para>
    /// The maximum of the two, per edge. An explicit <see cref="MinimumAxisSpace"/> is a floor the
    /// caller set and is never reduced; a label that needs more than that floor raises the
    /// request rather than being drawn into a band it does not fit.
    /// </para>
    /// <para>
    /// This widens the <em>request</em> only. Every layout guarantee survives intact,
    /// because the request has always been something the resolver may cut: the available area
    /// still comes first, the minimum plot second, axis space third and padding fourth, and a
    /// label asking for more cannot take a single DIP that the minimum plot is entitled to. When
    /// there is not enough room, the effective axis space comes back smaller than the labels
    /// wanted — which is the documented, expected outcome, not a failure.
    /// </para>
    /// <para>
    /// <see cref="MinimumAxisSpace"/> itself is not written to. The caller's value is their own, the
    /// same way the layout leaves requested padding untouched while shrinking its effect.
    /// </para>
    /// </remarks>
    private Thickness RequestedAxisSpace(Thickness required)
    {
        var requested = MinimumAxisSpace;

        return new Thickness(
            Math.Max(requested.Left, required.Left),
            Math.Max(requested.Top, required.Top),
            Math.Max(requested.Right, required.Right),
            Math.Max(requested.Bottom, required.Bottom));
    }

    /// <summary>
    /// How much axis thickness the ticks and labels need on each edge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the two edges the axes actually occupy are non-zero.
    /// </para>
    /// <para>
    /// <b>Visibility is not consulted.</b> It was at first, on the reasoning that an invisible
    /// axis draws nothing and so needs no room for what it does not draw. That reasoning
    /// contradicts the layout rule: visibility is a drawing
    /// concern, the space stays reserved either way, and hiding an axis must not move or resize
    /// the plot. Gating this on visibility would have made the plot jump the moment an axis was
    /// hidden — a layout change triggered by a drawing property, which is exactly what the layout
    /// rule says it must not be.
    /// </para>
    /// <para>
    /// So the room a label needs is reserved whether or not the label is drawn, the same way
    /// <see cref="MinimumAxisSpace"/> is. <c>ACTL-VIS</c> pins it.
    /// </para>
    /// <para>
    /// <b>The labels' reach is kept</b>. Every arrange asks this, and placing
    /// every label at the origin to find the widest would cost a thousand placements per resize step on a dense axis. The
    /// reach depends only on the measured text, the rotation, the anchor and the side, and is dropped when one of
    /// those changes; the gap and the tick length are added afterwards, as they always were.
    /// </para>
    /// </remarks>
    private Thickness ComputeRequiredAxisSpace()
    {
        // One reach per axis: a horizontal range re-measures the bottom or top labels and finds their
        // reach again, and keeps the left or right labels' reach — and their text — as they were.
        if (_horizontalLabelExtent is null || _verticalLabelExtent is null)
        {
            EnsureLabelTexts();
        }

        var built = false;

        if (_horizontalLabelExtent is not { } horizontalExtent)
        {
            horizontalExtent = MaximumOutwardExtent(_horizontalLabelMetrics, XAxisLabelAnchor, XAxisLabelRotation, XAxisPosition.ToSide());
            _horizontalLabelExtent = horizontalExtent;
            _cacheCounters.LabelXExtentBuilds++;
            built = true;
        }
        else
        {
            _cacheCounters.LabelXExtentHits++;
        }

        if (_verticalLabelExtent is not { } verticalExtent)
        {
            verticalExtent = MaximumOutwardExtent(_verticalLabelMetrics, YAxisLabelAnchor, YAxisLabelRotation, YAxisPosition.ToSide());
            _verticalLabelExtent = verticalExtent;
            _cacheCounters.LabelYExtentBuilds++;
            built = true;
        }
        else
        {
            _cacheCounters.LabelYExtentHits++;
        }

        if (built)
        {
            _cacheCounters.LabelExtentBuilds++;
        }
        else
        {
            _cacheCounters.LabelExtentHits++;
        }

        var extents = (Horizontal: horizontalExtent, Vertical: verticalExtent);

        double left = 0, top = 0, right = 0, bottom = 0;

        // The reach is kept only while the text it was measured from is current, so the text counts are too.
        if (_horizontalLabelTexts.Count > 0)
        {
            var position = XAxisPosition;
            var side = position.ToSide();
            var thickness = AxisLabelGeometry.RequiredAxisThickness(
                MajorTickLength,
                LabelGap,
                extents.Horizontal);

            if (side == AxisSide.Bottom)
            {
                bottom = thickness;
            }
            else
            {
                top = thickness;
            }
        }

        if (_verticalLabelTexts.Count > 0)
        {
            var position = YAxisPosition;
            var side = position.ToSide();
            var thickness = AxisLabelGeometry.RequiredAxisThickness(
                MajorTickLength,
                LabelGap,
                extents.Vertical);

            if (side == AxisSide.Left)
            {
                left = thickness;
            }
            else
            {
                right = thickness;
            }
        }

        return new Thickness(left, top, right, bottom);
    }

    /// <summary>The largest outward reach among one edge's labels.</summary>
    private static double MaximumOutwardExtent(
        LabelMetrics[] metrics,
        LabelAnchor anchor,
        LabelRotation rotation,
        AxisSide side)
    {
        var largest = 0d;

        foreach (var measured in metrics)
        {
            var placement = AxisLabelGeometry.Place(
                measured,
                anchor,
                rotation,
                side,
                new Point(0, 0));

            if (placement.OutwardExtent > largest)
            {
                largest = placement.OutwardExtent;
            }
        }

        return largest;
    }

    /// <summary>
    /// Computes the layout result for a given available area.
    /// </summary>
    private ChartLayoutResult Calculate(Size available, Thickness requestedAxisSpace)
        => ChartLayoutCalculator.Calculate(new ChartLayoutInput(
            available,
            Padding,
            MinimumPlotWidth,
            MinimumPlotHeight,
            MinimumPlotRatio,
            requestedAxisSpace));

    /// <summary>
    /// Records the calculator's plot bounds (inside the border), effective padding and effective axis space.
    /// </summary>
    /// <remarks>
    /// The public <see cref="PlotBounds"/> is published by <see cref="ArrangeOverride"/>, in the control's own space.
    /// </remarks>
    private void UpdateLayoutResults(in ChartLayoutResult result)
    {
        // Deliberately not Rect.Empty for a degenerate plot: WPF defines that as
        // X/Y positive infinity with negative-infinity extents, which would hand
        // consumers non-finite, negative bounds. A zero-sized rect keeps the
        // guarantee that the bounds are always finite and never negative.
        LayoutPlotBounds = result.PlotBounds;
        SetValue(EffectivePaddingPropertyKey, result.EffectivePadding);
        SetValue(EffectiveAxisSpacePropertyKey, result.EffectiveAxisSpace);
    }

    /// <summary>
    /// Places every label against its own tick, for the label layer to draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The anchor point for a label is derived from the same coordinate helper the tick mark
    /// uses, so a label and its tick cannot end up computed from two expressions that drift
    /// apart. From that coordinate the chain is fixed: plot edge, then the tick's drawn length,
    /// then the gap, then the label.
    /// </para>
    /// <para>
    /// The tick length used here is the <em>effective</em> one — what the band actually granted,
    /// which can be zero. A label then sits one gap from the plot edge rather than one gap from
    /// a mark that was never drawn, which keeps the gap meaning the same thing in both cases.
    /// </para>
    /// <para>
    /// Every tick with a label gets one. There is no path through this method that drops a label,
    /// shortens it or re-angles it.
    /// </para>
    /// <para>
    /// Called by the layer when it renders a stale frame, once per frame however many changes came before it
    ///; never by a property change directly.
    /// </para>
    /// <para>
    /// <b>Three caches, one composition</b>. The measured text (<see cref="EnsureLabelTexts"/>), the
    /// placements, and the brush the text is drawn with are each kept until something they are drawn from changes:
    /// the text until the tick values, the font or the DPI do; the placements until the text, the plot, the bands,
    /// an axis's position or visibility, a rotation, an anchor or the gap do; the brush until
    /// <see cref="Control.Foreground"/> does. A composition makes again only what was dropped — a new Foreground
    /// re-colours the measured text in place and returns the very placements it had.
    /// </para>
    /// <para>
    /// <b>Two halves, one frame</b>. The text, the placements and the reach are each kept per axis, and
    /// the frame is composed from both halves: a horizontal range measures and places the bottom or top labels again
    /// and hands the frame the left or right labels' placements — the very list, the very measured text — unchanged.
    /// </para>
    /// </remarks>
    private LabelFrame BuildLabels()
    {
        _counters.LabelBuilds++;

        EnsureLabelTexts();

        var built = false;

        if (_horizontalLabelPlacements is not { } horizontal)
        {
            horizontal = PlaceHorizontalLabels();
            _horizontalLabelPlacements = horizontal;
            _cacheCounters.LabelXGeometryBuilds++;
            built = true;
        }
        else
        {
            _cacheCounters.LabelXGeometryHits++;
        }

        if (_verticalLabelPlacements is not { } vertical)
        {
            vertical = PlaceVerticalLabels();
            _verticalLabelPlacements = vertical;
            _cacheCounters.LabelYGeometryBuilds++;
            built = true;
        }
        else
        {
            _cacheCounters.LabelYGeometryHits++;
        }

        if (built)
        {
            _cacheCounters.LabelGeometryBuilds++;
        }
        else
        {
            _cacheCounters.LabelGeometryHits++;
        }

        if (_labelStyleStale)
        {
            RestyleLabels();
        }
        else
        {
            _cacheCounters.LabelStyleHits++;
        }

        _joinedLabels = JoinedList<PlacedLabel>.Of(horizontal, vertical, _joinedLabels);

        // Every text carries the current brush now (measured with it, or re-coloured above): the frame says which.
        return new LabelFrame(horizontal, vertical, Foreground ?? Brushes.Transparent) { Labels = _joinedLabels };
    }

    /// <summary>
    /// Hands the measured text the current <see cref="Control.Foreground"/>, without measuring it again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The label style refresh. <see cref="FormattedText"/> carries the brush it is drawn with, but
    /// a new brush does not mean measuring every label again. A brush changes no glyph and no advance, so the text
    /// is the control's own to re-colour in place: the placements, and the outward reach the layout reads, were made
    /// from metrics no brush reaches.
    /// </para>
    /// <para>
    /// The caller's brush is handed over as it is, never frozen, exactly as the measurement always did; a mutable one
    /// is drawn live, so a change inside it shows at the next render whether or not it is reported here.
    /// </para>
    /// </remarks>
    private void RestyleLabels()
    {
        var brush = Foreground ?? Brushes.Transparent;

        foreach (var text in _horizontalLabelTexts)
        {
            text.SetForegroundBrush(brush);
        }

        foreach (var text in _verticalLabelTexts)
        {
            text.SetForegroundBrush(brush);
        }

        // Every text carries this brush now: the styles they may be reused in say so.
        _horizontalLabelStyle = _horizontalLabelStyle is { } horizontal ? horizontal with { Brush = brush } : null;
        _verticalLabelStyle = _verticalLabelStyle is { } vertical ? vertical with { Brush = brush } : null;
        _labelStyleStale = false;
        _cacheCounters.LabelStyleBuilds++;
    }

    /// <summary>Places the horizontal axis's labels against the current plot and band, from the measured text.</summary>
    /// <remarks>The X half of the label placements: it reads the horizontal axis's inputs and no other.</remarks>
    private List<PlacedLabel> PlaceHorizontalLabels()
    {
        var plot = _tickPlotBounds;
        var placed = new List<PlacedLabel>(_horizontalLabelTexts.Count);

        if (IsXAxisVisible && _horizontalLabelTexts.Count > 0 && plot.Width > 0)
        {
            var position = XAxisPosition;
            var side = position.ToSide();
            var band = side == AxisSide.Bottom ? _tickAxisSpace.Bottom : _tickAxisSpace.Top;
            var reach = AxisTickPlacement.EffectiveTickLength(MajorTickLength, band) + NonNegativeGap();
            var edge = side == AxisSide.Bottom ? plot.Bottom : plot.Top;
            var outward = side == AxisSide.Bottom ? reach : -reach;
            var ticks = HorizontalTicks();
            var anchor = XAxisLabelAnchor;
            var rotation = XAxisLabelRotation;

            for (var i = 0; i < ticks.Count && i < _horizontalLabelTexts.Count; i++)
            {
                var point = new Point(AxisTickPlacement.HorizontalCoordinate(plot, ticks[i]), edge + outward);

                placed.Add(Placed(
                    _horizontalLabelTexts[i],
                    AxisLabelGeometry.Place(_horizontalLabelMetrics[i], anchor, rotation, side, point)));
            }
        }

        return placed;
    }

    /// <summary>Places the vertical axis's labels against the current plot and band, from the measured text.</summary>
    /// <remarks>The Y half of the label placements: it reads the vertical axis's inputs and no other.</remarks>
    private List<PlacedLabel> PlaceVerticalLabels()
    {
        var plot = _tickPlotBounds;
        var placed = new List<PlacedLabel>(_verticalLabelTexts.Count);

        if (IsYAxisVisible && _verticalLabelTexts.Count > 0 && plot.Height > 0)
        {
            var position = YAxisPosition;
            var side = position.ToSide();
            var band = side == AxisSide.Left ? _tickAxisSpace.Left : _tickAxisSpace.Right;
            var reach = AxisTickPlacement.EffectiveTickLength(MajorTickLength, band) + NonNegativeGap();
            var edge = side == AxisSide.Left ? plot.Left : plot.Right;
            var outward = side == AxisSide.Left ? -reach : reach;
            var ticks = VerticalTicks();
            var anchor = YAxisLabelAnchor;
            var rotation = YAxisLabelRotation;

            for (var i = 0; i < ticks.Count && i < _verticalLabelTexts.Count; i++)
            {
                var point = new Point(edge + outward, AxisTickPlacement.VerticalCoordinate(plot, ticks[i]));

                placed.Add(Placed(
                    _verticalLabelTexts[i],
                    AxisLabelGeometry.Place(_verticalLabelMetrics[i], anchor, rotation, side, point)));
            }
        }

        return placed;
    }

    /// <summary>One label with its placement and that placement's transform, frozen once for every redraw.</summary>
    private static PlacedLabel Placed(FormattedText text, LabelPlacement placement)
    {
        var transform = new MatrixTransform(placement.Transform);

        transform.Freeze();

        return new PlacedLabel(text, placement) { Transform = transform };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Text is measured against the live scaling factor, so moving the window to a display with a
    /// different one invalidates every measurement. Without this the labels keep the previous
    /// monitor's metrics and the axis band keeps the thickness computed from them — stale in the
    /// way that looks almost right, which is the hard kind to notice.
    /// </remarks>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        // The measurement, the placements and the reach are dropped; the tick marks, in DIP, are not, unless the
        // arrange moves the bands.
        InvalidateLabelCache();

        // The lens labels are measured against the same DPI.
        InvalidateLensLabels();

        // So is the width of the columns a reduced series is built in.
        var layers = ChartLayers.Axis | ChartLayers.Layout | ChartLayers.Lens;

        if (HasReducedSeries())
        {
            _seriesEpoch++;
            _lensEpochY++;
            layers |= ChartLayers.Series;
        }

        Recompose(layers);

        // The tooltip text is measured against the same DPI.
        RefreshInteraction();
    }

    /// <summary>The label gap, guarded against a transient non-finite binding.</summary>
    private double NonNegativeGap()
    {
        var gap = LabelGap;

        return double.IsFinite(gap) && gap > 0 ? gap : 0;
    }

    private static void OnClipToPlotAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        control.ApplyPlotClip();

        // The clip is applied when the frame is drawn, not placed into the geometry: every series'
        // geometry is reused, and only the frame is composed again with the new setting. Only a reduced series has left
        // out what the clip would hide, and has to be built again. The lens has a clip of its own, whatever this says.
        if (control.HasReducedSeries())
        {
            control._seriesEpoch++;
        }

        control.Recompose(ChartLayers.Series);
    }

    /// <summary>
    /// Applies or removes the plot-area clip on the content host.
    /// </summary>
    /// <remarks>
    /// The clip is set on the content host, whose bounds are the plot area, so it
    /// constrains plot content only. Clipping never feeds back into the layout
    /// calculation.
    /// </remarks>
    private void ApplyPlotClip()
    {
        if (ContentHost is not { } host)
        {
            return;
        }

        host.ClipToBounds = ClipToPlotArea;
    }

    /// <summary>
    /// Accepts a finite, non-negative minimum extent.
    /// </summary>
    private static bool IsValidMinimumExtent(object value)
        => value is double d && double.IsFinite(d) && d >= 0;

    /// <summary>
    /// Accepts a finite ratio between 0 and 1 inclusive.
    /// </summary>
    private static bool IsValidRatio(object value)
        => value is double d && double.IsFinite(d) && d is >= 0 and <= 1;

    /// <summary>
    /// Accepts an axis space whose every edge is finite and non-negative.
    /// </summary>
    /// <remarks>
    /// Rejected at the property rather than silently clamped in the calculation. A
    /// negative or NaN edge is a caller mistake, and failing at the point of assignment
    /// says so, while a silent clamp would hide it until the plot looked wrong.
    /// </remarks>
    private static bool IsValidAxisSpace(object value)
        => value is Thickness t
        && IsValidEdge(t.Left) && IsValidEdge(t.Top)
        && IsValidEdge(t.Right) && IsValidEdge(t.Bottom);

    /// <summary>Accepts a finite, non-negative single edge value.</summary>
    private static bool IsValidEdge(double value) => double.IsFinite(value) && value >= 0;

    /// <summary>A solid brush the library owns, frozen: it is shared by every instance and can never be changed.</summary>
    private static SolidColorBrush FrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));

        brush.Freeze();

        return brush;
    }

    /// <summary>Accepts the two defined edges only, so a cast integer cannot pick a third.</summary>
    private static bool IsDefinedHorizontalAxisPosition(object value)
        => value is HorizontalAxisPosition position && position is HorizontalAxisPosition.Bottom or HorizontalAxisPosition.Top;

    /// <summary>Accepts the two defined edges only, so a cast integer cannot pick a third.</summary>
    private static bool IsDefinedVerticalAxisPosition(object value)
        => value is VerticalAxisPosition position && position is VerticalAxisPosition.Left or VerticalAxisPosition.Right;

    /// <summary>Accepts the four defined angles only, so a cast integer cannot pick a fifth.</summary>
    private static bool IsDefinedLabelRotation(object value)
        => value is LabelRotation rotation && rotation is LabelRotation.Degree0 or LabelRotation.Degree90 or LabelRotation.Degree180 or LabelRotation.Degree270;

    /// <summary>Accepts the three defined anchors only, so a cast integer cannot pick a fourth.</summary>
    private static bool IsDefinedLabelAnchor(object value)
        => value is LabelAnchor anchor && anchor is LabelAnchor.Start or LabelAnchor.Center or LabelAnchor.End;

    /// <summary>Accepts any finite double, including negative ones.</summary>
    /// <remarks>
    /// Range endpoints may legitimately be negative, so only NaN and infinity are rejected.
    /// An endpoint that is not a number has no position on any scale, and storing it would
    /// push the fault downstream into geometry.
    /// </remarks>
    private static bool IsFiniteDouble(object value) => value is double d && double.IsFinite(d);

    /// <summary>Accepts a target tick count within the supported band.</summary>
    private static bool IsValidTargetTickCount(object value)
        => value is int count && count >= 1 && count <= MaximumTargetTickCount;

    /// <summary>Accepts a finite, non-negative grid thickness.</summary>
    /// <remarks>
    /// Refused at the property, like every other bad numeric input here, rather than clamped where
    /// the pen is built. Zero is accepted: a pen of no width is a legitimate way to draw nothing.
    /// </remarks>
    private static bool IsValidGridThickness(object value) => value is double d && IsValidEdge(d);

    /// <summary>Redraws the grid when a grid-only input changes.</summary>
    /// <remarks>
    /// <para>
    /// Visibility, brush and thickness change what is painted and nothing else, so they invalidate
    /// no layout — a grid toggled on or off cannot move the plot, and the validation suite holds the
    /// plot rectangle and the axis space to exact equality across the toggle to prove it. The grid
    /// is rebuilt against the plot bounds of the last arrange, which are current. Only the grid is
    /// marked: no series, reference line, tick or label is drawn from these.
    /// </para>
    /// <para>
    /// A brush change drops the cached pen, because it may be a change inside a mutable brush: WPF
    /// reports it here as a sub-property change, with the same brush reference and a new colour, and
    /// the pen holds a snapshot taken before it.
    /// </para>
    /// <para>
    /// <b>Style or geometry</b>. The brush and the thickness are style: the pen is made again and the
    /// lines already placed are stroked with it — no line is placed again. Visibility is geometry: it decides which
    /// lines exist, so the lines are placed again.
    /// </para>
    /// </remarks>
    private static void OnGridInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        if (e.Property == MajorGridBrushProperty)
        {
            control._gridPen = null;
        }

        // Horizontal lines are drawn from the vertical axis's ticks, vertical lines from the horizontal axis's: each
        // visibility drops its own half of the grid, and its own part of the lens's. The lens
        // strokes with the main grid pen, so a brush or a thickness only composes it again.
        if (e.Property == IsHorizontalMajorGridVisibleProperty)
        {
            control._lensY = null;
            control.Invalidate(ChartLayers.Grid, ValueAxes.Vertical);
        }
        else if (e.Property == IsVerticalMajorGridVisibleProperty)
        {
            control._lensX = null;
            control.Invalidate(ChartLayers.Grid, ValueAxes.Horizontal);
        }
        else
        {
            // The thickness is compared where the pen is taken (GridPen); the brush was dropped above.
            control.Recompose(ChartLayers.Grid);
        }

        control.Recompose(ChartLayers.Lens);
    }

    /// <summary>Accepts a finite, non-negative label gap.</summary>
    /// <remarks>
    /// Zero is legitimate — a label flush against its tick — so only negative and non-finite
    /// values are refused. A negative gap would pull the label back across the tick and into
    /// the plot, which is a different feature, not a smaller gap.
    /// </remarks>
    private static bool IsValidLabelGap(object value) => value is double d && IsValidEdge(d);

    /// <summary>Re-derives everything drawn from a range when one of its endpoints changes.</summary>
    /// <remarks>
    /// <para>
    /// A range is the scale every layer is placed by: the ticks, and the grid lines and labels on them; the origin
    /// and reference lines, which a range change can also bring into the plot or take out of it; and every series
    /// point. All of them are marked. It invalidates arrange as well: a different range produces different label
    /// text, which measures differently, which changes the axis thickness the labels ask for and so the plot
    /// rectangle — the same correction <see cref="Control.BorderThickness"/> needed.
    /// </para>
    /// <para>
    /// Marked, not built: every layer is built once, when it is rendered after the arrange, against
    /// the plot that arrange computes, never here against the previous plot and again after the arrange.
    /// </para>
    /// <para>
    /// <b>One axis, not both</b>. The same layers are marked, but inside each only the half drawn from the
    /// axis whose range changed is placed again, and only that axis's labels are measured again. A horizontal range —
    /// a chart scrolling with its data — places the vertical grid lines, the X reference lines, the vertical origin, the
    /// series and the bottom or top axis, and reuses the horizontal grid lines, the Y reference lines, the horizontal
    /// origin and the left or right axis, measured text and all. Should the new labels move the plot, the arrange
    /// drops both halves, as it does for any move of the plot.
    /// </para>
    /// </remarks>
    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;
        var axis = AxisOf(e.Property);

        control.InvalidateLabelCache(axis);
        control.Invalidate(ChartLayers.Geometry | ChartLayers.Layout, axis);
    }

    /// <summary>Re-derives the ticks, and the grid lines and labels on them, when a target tick count changes.</summary>
    /// <remarks>
    /// The target chooses the tick values and nothing else: the origin and reference lines and the series are
    /// placed by the range, which has not changed, and are not marked. The labels can measure differently, so the
    /// layout is asked for as it is for a range. Only the half of the grid and the axis that one
    /// target's axis draws is placed again.
    /// </remarks>
    private static void OnTargetTickCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;
        var axis = AxisOf(e.Property);

        control.InvalidateLabelCache(axis);
        control.Invalidate(ChartLayers.Grid | ChartLayers.Axis | ChartLayers.Layout, axis);

        // The lens's horizontal grid lines stand at the vertical axis's ticks; its X ticks have a target of
        // their own (LensTargetTickCount), which no main target reaches.
        if (axis == ValueAxes.Vertical)
        {
            control._lensY = null;
            control.Recompose(ChartLayers.Lens);
        }
    }

    /// <summary>Moves an axis to its new edge.</summary>
    /// <remarks>
    /// The ticks and labels move; the space they ask for moves to the other edge, which moves the plot, so the
    /// layout is asked for. The text is the same text and is not measured again. Any other layer is marked by the
    /// arrange if, and only if, the plot moved. The labels' reach is measured toward the new side, so it is dropped
    /// with the axis geometry, for the axis that moved alone.
    /// </remarks>
    private static void OnAxisPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        if (e.Property == XAxisPositionProperty)
        {
            control._horizontalLabelExtent = null;

            // The lens's X axis moves to the same edge of the lens.
            control._lensX = null;
            control._lensLabelPlacements = null;
            control.Invalidate(ChartLayers.Axis | ChartLayers.Layout, ValueAxes.Horizontal);
            control.Recompose(ChartLayers.Lens);
        }
        else
        {
            control._verticalLabelExtent = null;
            control.Invalidate(ChartLayers.Axis | ChartLayers.Layout, ValueAxes.Vertical);
        }
    }

    /// <summary>Shows or hides an axis's ticks and labels.</summary>
    /// <remarks>
    /// Drawing only: the space an axis's labels need is reserved whether or not it is drawn
    /// (<see cref="ComputeRequiredAxisSpace"/>), so hiding one cannot move the plot, and no layout is asked for.
    /// It does not re-run the arrange or rebuild every layer, since the plot would always be the same. Only
    /// the axis shown or hidden places its ticks and labels again.
    /// </remarks>
    private static void OnAxisVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;
        var horizontal = e.Property == IsXAxisVisibleProperty;

        control.Invalidate(ChartLayers.Axis, horizontal ? ValueAxes.Horizontal : ValueAxes.Vertical);

        // The lens's X ticks and labels are the horizontal axis's, shown or hidden with it.
        if (horizontal)
        {
            control._lensX = null;
            control._lensLabelPlacements = null;
            control.Recompose(ChartLayers.Lens);
        }
    }

    /// <summary>Drops the measured label text, so the next pass rebuilds it.</summary>
    /// <param name="axes">The axes whose text is dropped; both unless one is named.</param>
    /// <remarks>
    /// With it go everything made from the measurement: the placements and the labels' reach. The tick
    /// marks are not made from it and are kept. The other axis's text, placements and reach are not touched.
    /// </remarks>
    private void InvalidateLabelCache(ValueAxes axes = ValueAxes.Both)
    {
        if ((axes & ValueAxes.Horizontal) != 0)
        {
            _horizontalLabelTextsDirty = true;
            _horizontalLabelPlacements = null;
            _horizontalLabelExtent = null;
        }

        if ((axes & ValueAxes.Vertical) != 0)
        {
            _verticalLabelTextsDirty = true;
            _verticalLabelPlacements = null;
            _verticalLabelExtent = null;
        }
    }

    /// <summary>Rebuilds the measured label text if anything it depends on has changed.</summary>
    /// <remarks>
    /// <para>
    /// Measuring text is the expensive part of labelling, and a resize changes nothing it
    /// depends on: the tick values, the font, the brush, the culture and the DPI are all
    /// independent of the plot rectangle. Caching here is what keeps a continuous drag from
    /// re-shaping every string on every frame.
    /// </para>
    /// <para>
    /// The cache is invalidated explicitly by each input rather than by comparing a key, because
    /// one of the inputs is the live DPI and another is a brush instance; a key over those is
    /// either wrong or as expensive as the work it guards.
    /// </para>
    /// <para>
    /// The brush is not one of them: a new <see cref="Control.Foreground"/> is handed to the
    /// text already measured (<see cref="RestyleLabels"/>).
    /// </para>
    /// </remarks>
    private void EnsureLabelTexts()
    {
        if (!_horizontalLabelTextsDirty && !_verticalLabelTextsDirty)
        {
            _cacheCounters.LabelMeasurementHits++;
            _cacheCounters.LabelXMeasurementHits++;
            _cacheCounters.LabelYMeasurementHits++;

            return;
        }

        _counters.LabelTextBuilds++;
        _cacheCounters.LabelMeasurements++;

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var brush = Foreground ?? Brushes.Transparent;

        // CurrentCulture, deliberately and documented: a label is user-facing text, so a decimal
        // comma belongs where the user's settings ask for one. The validation suite fixes the
        // culture instead of inheriting it, so its expectations do not depend on the machine.
        var culture = CultureInfo.CurrentCulture;

        var fontSize = FontSize;

        // Per axis: a horizontal range measures the horizontal axis's labels and keeps the vertical
        // axis's text, its metrics, its placements and its reach as they were. Nothing either axis's text is measured
        // from belongs to the other.
        var both = _horizontalLabelTextsDirty && _verticalLabelTextsDirty;

        // The style the text is shaped in now. A string the last build of an axis shaped in this very style — carrying
        // this very brush — is taken as it is. A text still waiting for a re-colour carries another brush, so
        // its style is not this one and it is not taken.
        var style = new LabelTextStyle(typeface, fontSize, pixelsPerDip, brush, culture);

        if (_horizontalLabelTextsDirty)
        {
            _horizontalLabelTextsDirty = false;
            _horizontalLabelTexts = BuildLabelTexts(HorizontalTicks(), style, _horizontalLabelStyle == style ? _horizontalLabelTexts : null);
            _horizontalLabelStyle = style;

            // The metrics, read once here: the placements and the reach are made from these, so a rotation
            // or a resize never asks a text for its size again.
            _horizontalLabelMetrics = [.. _horizontalLabelTexts.Select(AxisLabelMeasurement.MetricsOf)];
            _horizontalLabelPlacements = null;
            _horizontalLabelExtent = null;
            _cacheCounters.LabelXMeasurements++;
        }
        else
        {
            _cacheCounters.LabelXMeasurementHits++;
        }

        if (_verticalLabelTextsDirty)
        {
            _verticalLabelTextsDirty = false;
            _verticalLabelTexts = BuildLabelTexts(VerticalTicks(), style, _verticalLabelStyle == style ? _verticalLabelTexts : null);
            _verticalLabelStyle = style;
            _verticalLabelMetrics = [.. _verticalLabelTexts.Select(AxisLabelMeasurement.MetricsOf)];
            _verticalLabelPlacements = null;
            _verticalLabelExtent = null;
            _cacheCounters.LabelYMeasurements++;
        }
        else
        {
            _cacheCounters.LabelYMeasurementHits++;
        }

        // Measured with the current brush, so there is nothing to re-colour — unless one axis's text was kept, in
        // which case a pending re-colour still reaches it.
        if (both)
        {
            _labelStyleStale = false;
        }
    }

    /// <summary>One label per tick, shaped — or taken from the axis's last build where that shaped the same string.</summary>
    /// <param name="ticks">The axis's ticks.</param>
    /// <param name="style">The style to shape in.</param>
    /// <param name="previous">
    /// The axis's last texts, when they were shaped in exactly <paramref name="style"/> and every one carries its brush;
    /// null when nothing may be reused.
    /// </param>
    /// <remarks>
    /// <para>
    /// Shaping is nearly all of a label rebuild's cost, and a range change mostly keeps its strings: in a dense X scroll
    /// nearly every label is a string the frame before had shaped. A <see cref="FormattedText"/> made from the
    /// same string, typeface, size, brush, DPI and culture is the same text, so it is taken as it is — the very object,
    /// its glyphs, metrics, fallback and every text property unchanged.
    /// </para>
    /// <para>
    /// <b>Bounded by construction.</b> Only the axis's last list is consulted, and the new list replaces it: nothing
    /// outlives one build, so the texts kept never exceed the ticks drawn. No string is remembered across a style
    /// change, and a string the last build did not have is shaped as before.
    /// </para>
    /// </remarks>
    private IReadOnlyList<FormattedText> BuildLabelTexts(IReadOnlyList<Tick> ticks, LabelTextStyle style, IReadOnlyList<FormattedText>? previous)
    {
        if (ticks.Count == 0)
        {
            return [];
        }

        Dictionary<string, FormattedText>? shaped = null;

        if (previous is { Count: > 0 })
        {
            shaped = new Dictionary<string, FormattedText>(previous.Count, StringComparer.Ordinal);

            foreach (var text in previous)
            {
                shaped.TryAdd(text.Text, text);
            }
        }

        var texts = new List<FormattedText>(ticks.Count);

        // One label per tick, always. Nothing here decides a label is too crowded to draw: that
        // decision does not exist, and a label that overlaps its neighbour is a
        // legible fault the caller can see and act on, where a label silently missing is not.
        foreach (var tick in ticks)
        {
            var label = AxisLabelFormatter.Format(tick.Value, style.Culture);

            if (shaped is not null && shaped.TryGetValue(label, out var kept))
            {
                texts.Add(kept);
                _processingCounters.LabelTextsReused++;

                continue;
            }

            texts.Add(AxisLabelMeasurement.Create(label, style.Typeface, style.FontSize, style.Brush, style.PixelsPerDip, style.Culture));
            _processingCounters.LabelTextsShaped++;
        }

        return texts;
    }

    /// <summary>Everything a label's shaped text is made from but its string.</summary>
    /// <remarks>
    /// Compared by value: the typeface by its family, style, weight and stretch, the brush and the culture by identity,
    /// so a mutable brush changed in place — drawn live, never re-shaped — is still the same style.
    /// </remarks>
    private readonly record struct LabelTextStyle(Typeface Typeface, double FontSize, double PixelsPerDip, Brush Brush, CultureInfo Culture);

    /// <summary>
    /// Places the tick marks, for the tick layer to draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The three stages run in order here: generate values from the range, place them against
    /// the plot rectangle, then draw. Only the first is cached, because only the first is
    /// independent of layout — a resize re-places and redraws the same values rather than
    /// computing new ones.
    /// </para>
    /// <para>
    /// Called by the layer when it renders a stale frame, once per frame however many changes came before it
    ///.
    /// </para>
    /// <para>
    /// <b>Placed once, stroked as often as needed</b>. The marks are kept until the plot, the axis
    /// bands, an axis's position or visibility, or the tick values change — everything
    /// <see cref="Invalidate"/> drops them for — and a composition otherwise only takes the pen: a new
    /// <see cref="Control.Foreground"/>, a font or a rotation re-strokes the marks already placed. They still move
    /// on every resize; one plot's marks are kept, never several.
    /// </para>
    /// <para>
    /// <b>Two halves</b>: the horizontal axis's marks and the vertical axis's are kept apart, each dropped
    /// only by what reaches its own axis — its tick values, its band, its position or visibility — or by a move of the
    /// plot, which reaches both.
    /// </para>
    /// </remarks>
    private TickFrame BuildTicks()
    {
        _counters.TickBuilds++;

        // Per axis: a horizontal range places the bottom or top marks again and reuses the left or
        // right ones, the very list.
        var built = false;

        if (_horizontalTickMarks is not { } horizontal)
        {
            horizontal = PlaceHorizontalTicks();
            _horizontalTickMarks = horizontal;
            _cacheCounters.TickXGeometryBuilds++;
            built = true;
        }
        else
        {
            _cacheCounters.TickXGeometryHits++;
        }

        if (_verticalTickMarks is not { } vertical)
        {
            vertical = PlaceVerticalTicks();
            _verticalTickMarks = vertical;
            _cacheCounters.TickYGeometryBuilds++;
            built = true;
        }
        else
        {
            _cacheCounters.TickYGeometryHits++;
        }

        if (built)
        {
            _cacheCounters.TickGeometryBuilds++;
        }
        else
        {
            _cacheCounters.TickGeometryHits++;
        }

        _joinedTickMarks = JoinedList<TickMark>.Of(horizontal, vertical, _joinedTickMarks);

        return new TickFrame(horizontal, vertical, TickPen()) { Marks = _joinedTickMarks };
    }

    /// <summary>Places the horizontal axis's tick marks against the current plot and band: the X half.</summary>
    private List<TickMark> PlaceHorizontalTicks()
    {
        var ticks = HorizontalTicks();
        var marks = new List<TickMark>(ticks.Count);

        if (IsXAxisVisible)
        {
            var position = XAxisPosition;
            var band = position == Axes.HorizontalAxisPosition.Bottom
                ? _tickAxisSpace.Bottom
                : _tickAxisSpace.Top;

            AxisTickPlacement.AppendHorizontal(
                marks,
                _tickPlotBounds,
                position,
                AxisTickPlacement.EffectiveTickLength(MajorTickLength, band),
                ticks);
        }

        return marks;
    }

    /// <summary>Places the vertical axis's tick marks against the current plot and band: the Y half.</summary>
    private List<TickMark> PlaceVerticalTicks()
    {
        var ticks = VerticalTicks();
        var marks = new List<TickMark>(ticks.Count);

        if (IsYAxisVisible)
        {
            var position = YAxisPosition;
            var band = position == Axes.VerticalAxisPosition.Left
                ? _tickAxisSpace.Left
                : _tickAxisSpace.Right;

            AxisTickPlacement.AppendVertical(
                marks,
                _tickPlotBounds,
                position,
                AxisTickPlacement.EffectiveTickLength(MajorTickLength, band),
                ticks);
        }

        return marks;
    }

    /// <summary>
    /// Builds the major grid from the axes' ticks, for the grid layer to draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ticks come from <see cref="HorizontalTicks"/> and <see cref="VerticalTicks"/> — the same
    /// cached sequences the tick marks and labels are built from — and are placed against the same
    /// <c>_tickPlotBounds</c>. Nothing is generated for the grid. A grid line therefore cannot
    /// disagree with its tick, because it is not a second answer to the same question.
    /// </para>
    /// <para>
    /// <b>Axis visibility is not consulted</b>, deliberately, and unlike the tick and label paths.
    /// <see cref="IsXAxisVisible"/> and <see cref="IsYAxisVisible"/> govern the axis
    /// visuals — marks and text. The grid is plot content that reads the axes' tick data, which is
    /// generated whether or not an axis is drawn, so a hidden axis leaves its grid in place. Only
    /// the grid's own visibility properties remove it.
    /// </para>
    /// <para>
    /// Nor are rotation, anchor, gap or font consulted. They move labels; they can widen an axis band
    /// and so move the plot, and the grid follows the plot — but nothing about a label reaches a grid
    /// line by any other route.
    /// </para>
    /// <para>
    /// Called by the layer when it renders a stale frame, once per frame however many changes came before it
    ///.
    /// </para>
    /// <para>
    /// <b>Geometry and style apart</b>. The lines are kept until the plot, the tick values or a grid
    /// visibility changes; the pen until the brush, a change inside it, or the thickness does. A composition makes
    /// again only what was dropped, so a brush change strokes the very lines the last frame placed.
    /// </para>
    /// <para>
    /// <b>Two halves</b>. The horizontal lines, drawn from the vertical axis's ticks, and the vertical
    /// lines, drawn from the horizontal axis's, are kept apart: a horizontal range places the vertical lines again and
    /// hands the frame the horizontal lines it already had — the very list. A move of the plot drops both.
    /// </para>
    /// </remarks>
    private GridFrame BuildGrid()
    {
        _counters.GridBuilds++;

        var built = false;

        // Horizontal lines, from the vertical axis's ticks: the Y half.
        if (_horizontalGridLines is not { } horizontal)
        {
            horizontal = [];

            if (IsHorizontalMajorGridVisible)
            {
                MajorGridPlacement.AppendHorizontal(horizontal, _tickPlotBounds, VerticalTicks());
            }

            _horizontalGridLines = horizontal;
            _cacheCounters.GridYGeometryBuilds++;
            built = true;
        }
        else
        {
            _cacheCounters.GridYGeometryHits++;
        }

        // Vertical lines, from the horizontal axis's ticks: the X half.
        if (_verticalGridLines is not { } vertical)
        {
            vertical = [];

            if (IsVerticalMajorGridVisible)
            {
                MajorGridPlacement.AppendVertical(vertical, _tickPlotBounds, HorizontalTicks());
            }

            _verticalGridLines = vertical;
            _cacheCounters.GridXGeometryBuilds++;
            built = true;
        }
        else
        {
            _cacheCounters.GridXGeometryHits++;
        }

        if (built)
        {
            _cacheCounters.GridGeometryBuilds++;
        }
        else
        {
            _cacheCounters.GridGeometryHits++;
        }

        // Both halves were placed against the plot as it is now: a move of the plot drops both (Invalidate).
        return new GridFrame(horizontal, vertical, _tickPlotBounds, GridPen());
    }

    /// <summary>
    /// Builds the pen grid lines are stroked with, from <see cref="MajorGridBrush"/> and
    /// <see cref="MajorGridThickness"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built by <see cref="FrozenPen"/>, so a caller's mutable brush is drawn through a frozen
    /// snapshot and never frozen itself. Rebuilt when the brush is replaced, when the thickness
    /// changes, and when <see cref="OnGridInputChanged"/> drops it because the brush changed inside.
    /// Solid, with flat caps — WPF's defaults — so a line ends exactly at the plot edge and does not
    /// overshoot it by half its width. No pen at all for a null brush or a zero thickness, so neither
    /// records draw commands that paint nothing.
    /// </para>
    /// <para>
    /// The pen is not frozen directly over <see cref="MajorGridBrush"/>: freezing a pen freezes its
    /// brush in place, so a caller's mutable brush would become read-only behind their
    /// back and their next change to it would throw.
    /// </para>
    /// </remarks>
    private Pen? GridPen()
    {
        var brush = MajorGridBrush;
        var thickness = MajorGridThickness;

        if (brush is null || !(thickness > 0))
        {
            _cacheCounters.GridStyleHits++;

            return null;
        }

        if (_gridPen is not null && ReferenceEquals(_gridPenBrush, brush) && _gridPenThickness == thickness)
        {
            _cacheCounters.GridStyleHits++;

            return _gridPen;
        }

        var pen = FrozenPen(brush, thickness);

        _gridPenBrush = brush;
        _gridPenThickness = thickness;
        _gridPen = pen;
        _cacheCounters.GridStyleBuilds++;

        return pen;
    }

    /// <summary>Redraws the origin lines when an origin-only input changes.</summary>
    /// <remarks>
    /// Visibility, brush and thickness change what is painted and nothing else, so they invalidate no
    /// layout — the validation suite holds the plot rectangle, the axis space, every tick, label and grid
    /// line to exact equality across each of them. The pen is dropped rather than compared, because the
    /// change may be inside a mutable brush, where the reference is the same and the colour is not. Only the
    /// reference layer, which draws the origins, is marked.
    /// <para>
    /// A visibility change drops the origin geometry and a brush or thickness change the origin pen,
    /// and nothing else: the reference lines are reused, placed and stroked as they were, and the origins already
    /// placed are re-stroked.
    /// </para>
    /// </remarks>
    private static void OnOriginStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        // y = 0 is the horizontal line, x = 0 the vertical one; each visibility drops its own, in the lens as
        // well, which strokes both with the main origin pen.
        if (e.Property == IsHorizontalOriginLineVisibleProperty)
        {
            control._horizontalOrigin = null;
            control._lensY = null;
        }
        else if (e.Property == IsVerticalOriginLineVisibleProperty)
        {
            control._verticalOrigin = null;
            control._lensX = null;
        }
        else
        {
            control._originPenValid = false;
        }

        control.Recompose(ChartLayers.Reference | ChartLayers.Lens);
    }

    /// <summary>Redraws the reference lines when the collection, an item in it or the fallback brush changes.</summary>
    /// <remarks>
    /// <para>
    /// The collection is a <see cref="Freezable"/> held in a dependency property, so WPF reports every
    /// change inside it here as a sub-property change of <see cref="ReferenceLinesProperty"/>: an add, a
    /// remove, a clear, a change to any property of any line, and a change inside a mutable brush a line
    /// holds. No event is subscribed for it, so there is nothing to unsubscribe, and a removed line keeps
    /// no reference to this control.
    /// </para>
    /// <para>
    /// Drawing only, like the grid's inputs: no layout is invalidated, and only the reference layer is marked
    ///.
    /// </para>
    /// <para>
    /// <b>Which line changed, and how, is the line's to say</b>. The report does not tell one change
    /// from another, and nothing cached is dropped here. Each line counts its own geometry and style changes
    /// (<see cref="ChartReferenceLine.GeometryGeneration"/>, <see cref="ChartReferenceLine.StyleGeneration"/>), and the
    /// composition places again only a line whose geometry moved on and makes a pen again only for a line whose
    /// style did — one value change among five hundred lines places one line.
    /// </para>
    /// <para>
    /// The lens draws the same lines, kept per line by the same generations, so it is composed too and places
    /// again only the line that moved.
    /// </para>
    /// </remarks>
    private static void OnReferenceLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FastiraChartControl)d).Recompose(ChartLayers.Reference | ChartLayers.Lens);

    /// <summary>Re-strokes the reference lines that fall back on <see cref="ReferenceLineBrush"/>.</summary>
    /// <remarks>
    /// A style change: every reference pen is dropped, because any of them may be a snapshot of this
    /// brush — a replacement, or a change inside it — and every line placed is reused.
    /// </remarks>
    private static void OnReferenceLineBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        control._referencePens = [];

        // The lens takes each line's pen from the main frame: composed again, nothing placed again.
        control.Recompose(ChartLayers.Reference | ChartLayers.Lens);
    }

    /// <summary>
    /// Places the origin and reference lines against the current plot, for the reference layer to draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Placed against <c>_tickPlotBounds</c> — the rectangle the ticks and the grid are placed against —
    /// through <see cref="ReferenceLinePlacement"/>, which maps a value by the tick generator's own mapping
    /// and the tick marks' own coordinate expressions. Nothing here computes a scale.
    /// </para>
    /// <para>
    /// <b>The range decides, not the ticks.</b> An origin line exists when its range includes 0, and a
    /// reference line when its range includes its value, endpoints included. Whether a major tick falls on
    /// that value is never asked, and a value outside the range is never clamped onto an edge.
    /// </para>
    /// <para>
    /// <b>Axis visibility is not consulted</b>, for the reason the grid gives: these are plot content that
    /// read an axis's range, which exists whether or not the axis is drawn. Nor are rotation, anchor, gap
    /// or font: they can move the plot, and the lines follow the plot, by no other route.
    /// </para>
    /// <para>
    /// Called by the layer when it renders a stale frame, once per frame however many changes came before it
    ///.
    /// </para>
    /// <para>
    /// <b>Placed per line, kept per line</b>. Each line's placed geometry is kept with the
    /// <see cref="ChartReferenceLine.GeometryGeneration"/> it was placed at, in the epoch of the plot and ranges it
    /// was placed against; a composition places again only a line whose generation moved on, or every line once the
    /// range or the plot moved (<see cref="Invalidate"/> starts a new epoch). The pens are kept likewise, per brush
    /// and thickness, and made again for a line whose <see cref="ChartReferenceLine.StyleGeneration"/> moved on — a
    /// change inside its brush among them. The origins are one more part, with their own geometry and pen. Only the
    /// lines in the collection keep an entry, so a removed line's geometry goes with the next composition.
    /// </para>
    /// </remarks>
    private ReferenceFrame BuildReferenceLines()
    {
        _counters.ReferenceBuilds++;

        var plot = _tickPlotBounds;
        var horizontalMinimum = MinimumX;
        var horizontalMaximum = MaximumX;
        var verticalMinimum = MinimumY;
        var verticalMaximum = MaximumY;

        // The two origins are two parts: y = 0 is placed by the vertical range, x = 0 by the horizontal.
        var originBuilt = false;

        if (_horizontalOrigin is not { } horizontalOrigin)
        {
            horizontalOrigin = new OriginPart(
                IsHorizontalOriginLineVisible && ReferenceLinePlacement.TryHorizontal(plot, verticalMinimum, verticalMaximum, 0, out var y0) ? y0 : null);
            _horizontalOrigin = horizontalOrigin;
            _cacheCounters.OriginYGeometryBuilds++;
            originBuilt = true;
        }
        else
        {
            _cacheCounters.OriginYGeometryHits++;
        }

        if (_verticalOrigin is not { } verticalOrigin)
        {
            verticalOrigin = new OriginPart(
                IsVerticalOriginLineVisible && ReferenceLinePlacement.TryVertical(plot, horizontalMinimum, horizontalMaximum, 0, out var x0) ? x0 : null);
            _verticalOrigin = verticalOrigin;
            _cacheCounters.OriginXGeometryBuilds++;
            originBuilt = true;
        }
        else
        {
            _cacheCounters.OriginXGeometryHits++;
        }

        if (originBuilt)
        {
            _cacheCounters.OriginGeometryBuilds++;
        }
        else
        {
            _cacheCounters.OriginGeometryHits++;
        }

        var lines = ReferenceLines;
        var count = lines?.Count ?? 0;
        var placed = new List<PlacedReferenceLine>(count);
        var previous = _referenceEntries;
        var entries = new Dictionary<ChartReferenceLine, ReferenceEntry>(count);
        var previousPens = _referencePens;
        var pens = new Dictionary<(Brush Brush, double Thickness), Pen>();
        var fallback = ReferenceLineBrush;

        if (lines is not null)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i] is not { } line)
                {
                    continue;
                }

                // The same line twice in the collection is one entry, made at its first occurrence.
                var known = entries.TryGetValue(line, out var entry) || previous.TryGetValue(line, out entry);
                var restyled = !known || entry.StyleGeneration != line.StyleGeneration;

                // A line is placed by its own axis's range alone: an X-value line — a vertical screen line —
                // is valid until the horizontal range or the plot moves, a Y-value line until the vertical range or the
                // plot does. The axis it was placed on is kept with it, so a line moved to the other axis is never
                // judged by the wrong epoch (its generation moves on as well).
                var onX = line.Axis == ReferenceLineAxis.X;
                var epoch = onX ? _referenceEpochX : _referenceEpochY;

                if (known && entry.Epoch == epoch && entry.Axis == line.Axis && entry.GeometryGeneration == line.GeometryGeneration)
                {
                    _cacheCounters.ReferenceGeometryHits++;

                    if (onX)
                    {
                        _cacheCounters.ReferenceXGeometryHits++;
                    }
                    else
                    {
                        _cacheCounters.ReferenceYGeometryHits++;
                    }
                }
                else
                {
                    var geometry = default(PlotLine);
                    var hasLine = line.IsVisible
                        && (onX
                            ? ReferenceLinePlacement.TryVertical(plot, horizontalMinimum, horizontalMaximum, line.Value, out geometry)
                            : ReferenceLinePlacement.TryHorizontal(plot, verticalMinimum, verticalMaximum, line.Value, out geometry));

                    entry = new ReferenceEntry(epoch, line.Axis, line.GeometryGeneration, hasLine, geometry, entry.StyleGeneration);
                    _cacheCounters.ReferenceGeometryBuilds++;

                    if (onX)
                    {
                        _cacheCounters.ReferenceXGeometryBuilds++;
                    }
                    else
                    {
                        _cacheCounters.ReferenceYGeometryBuilds++;
                    }
                }

                entries[line] = entry with { StyleGeneration = line.StyleGeneration };

                if (entry.HasLine)
                {
                    placed.Add(new PlacedReferenceLine(i, entry.Line, ReferencePen(line.Stroke ?? fallback, line.StrokeThickness, restyled ? null : previousPens, pens)));
                }
                else if (!restyled)
                {
                    // Not drawn now — hidden, or out of its range — but its pen is carried while its style is
                    // unchanged, so drawing it again makes nothing.
                    Carry(line.Stroke ?? fallback, line.StrokeThickness, previousPens, pens);
                }
            }
        }

        _referenceEntries = entries;
        _referencePens = pens;

        return new ReferenceFrame(placed, horizontalOrigin.Line, verticalOrigin.Line, OriginPen(), plot);
    }

    /// <summary>The pen both origin lines are stroked with, built once per style.</summary>
    private Pen? OriginPen()
    {
        if (_originPenValid)
        {
            _cacheCounters.OriginStyleHits++;

            return _originPen;
        }

        _originPen = FrozenPen(OriginLineBrush, OriginLineThickness);
        _originPenValid = true;
        _cacheCounters.OriginStyleBuilds++;

        return _originPen;
    }

    /// <summary>
    /// The pen for one reference line's style, shared by every line with the same brush and thickness.
    /// </summary>
    /// <param name="brush">The line's brush, or the fallback.</param>
    /// <param name="thickness">The line's thickness.</param>
    /// <param name="previous">
    /// The pens the last composition used, or null when this line's style changed since: then only a pen made in this
    /// composition may serve it, never a snapshot from before the change.
    /// </param>
    /// <param name="current">The pens this composition has used so far.</param>
    private Pen? ReferencePen(
        Brush? brush,
        double thickness,
        Dictionary<(Brush Brush, double Thickness), Pen>? previous,
        Dictionary<(Brush Brush, double Thickness), Pen> current)
    {
        if (brush is null || !(thickness > 0))
        {
            _cacheCounters.ReferenceStyleHits++;

            return null;
        }

        var key = (brush, thickness);

        if (current.TryGetValue(key, out var pen) || (previous is not null && previous.TryGetValue(key, out pen)))
        {
            _cacheCounters.ReferenceStyleHits++;
        }
        else
        {
            pen = FrozenPen(brush, thickness)!;
            _cacheCounters.ReferenceStyleBuilds++;
        }

        current[key] = pen;

        return pen;
    }

    /// <summary>Redraws the series when the collection, a series in it or the fallback stroke changes.</summary>
    /// <remarks>
    /// <para>
    /// The collection is a <see cref="Freezable"/> held in a dependency property, so WPF reports every change
    /// inside it here as a sub-property change of <see cref="SeriesProperty"/>: an add, a remove, a clear, a
    /// change to any property of any series — new points among them — and a change inside a mutable brush a
    /// series holds. No event is subscribed for it, so there is nothing to unsubscribe, and a removed series
    /// keeps no reference to this control.
    /// </para>
    /// <para>
    /// Drawing only: no layout is invalidated, and only the series layer is marked.
    /// </para>
    /// <para>
    /// <b>Which series changed, and how, is the series' to say</b>. Nothing cached is dropped here.
    /// Each series counts its own point and style changes (<see cref="LineSeries.GeometryGeneration"/>,
    /// <see cref="LineSeries.StyleGeneration"/>), and the composition builds again only the geometry of a series
    /// whose points moved on and makes a pen again only for a series whose stroke did. A thousand appends to one of
    /// three series build that one series' geometry once; a stroke change builds none.
    /// </para>
    /// </remarks>
    private static void OnSeriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((FastiraChartControl)d).Recompose(ChartLayers.Series | ChartLayers.Lens);

    /// <summary>Re-strokes the series that fall back on <see cref="SeriesStroke"/>.</summary>
    /// <remarks>
    /// A style change: every series pen is dropped, because any of them may be a snapshot of this
    /// brush — a replacement, or a change inside it — and every series' geometry is reused.
    /// </remarks>
    private static void OnSeriesStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FastiraChartControl)d;

        control._seriesPens = [];

        // The lens takes each series' pen from the main frame: composed again, no geometry built.
        control.Recompose(ChartLayers.Series | ChartLayers.Lens);
    }

    /// <summary>How many times the series have been invalidated: asked to redraw.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, which checks that one data update — one append,
    /// one append of a thousand points, one replace, one clear — costs one invalidation, not one per point.
    /// It reads <see cref="Counters"/>. Internal: it is not a public API.
    /// </remarks>
    internal int SeriesInvalidations => _counters.SeriesInvalidations;

    /// <summary>How many times the series layer has been built: composed for a render.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, which checks that invalidations made before a
    /// render are built once, at the render, and never once per update. It reads
    /// <see cref="Counters"/>. Such a build
    /// builds only the geometry that changed, which <see cref="CacheCounters"/> counts per series. Internal: it is
    /// not a public API.
    /// </remarks>
    internal int SeriesGeometryBuilds => _counters.SeriesBuilds;

    /// <summary>
    /// Places every visible series against the current plot, for the drawing layer to stroke.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called by the layer when it renders a stale frame — once per frame, however many changes came before it
    /// — or when internal validation reads it. Marked by <see cref="Invalidate"/>: on a data, style, collection or clip
    /// change, a range change, and an arrange that moved the plot. Placed against <c>_tickPlotBounds</c> — the rectangle the ticks, the
    /// grid and the reference lines are placed against — through <see cref="SeriesPlacement"/>, which maps a
    /// value by the tick generator's own mapping and the tick marks' own coordinate expressions. Nothing here
    /// computes a scale.
    /// </para>
    /// <para>
    /// The control's two ranges are the only scale. A degenerate or inverted range, or a plot with no
    /// interior, draws no series, as it draws no grid line.
    /// </para>
    /// <para>
    /// <b>Axis visibility is not consulted</b>, for the reason the grid gives: series are plot content that read
    /// the ranges, which exist whether or not an axis is drawn.
    /// </para>
    /// <para>
    /// <b>Built per series, kept per series</b>. Each series' geometry is kept with the
    /// <see cref="LineSeries.GeometryGeneration"/> it was built at, in the epoch of the plot and ranges it was placed
    /// against. A composition builds again only a series whose points moved on, or every series once the range or
    /// the plot moved (<see cref="Invalidate"/> starts a new epoch). A style change — a stroke, a change inside it, a
    /// thickness, <see cref="SeriesStroke"/>, <see cref="ClipToPlotArea"/> — strokes the very geometry the last frame
    /// drew, except for a series with <see cref="LineSeries.IsRenderReductionEnabled"/>, whose geometry depends on the
    /// thickness and the clip. One geometry per series is kept, the latest, never a history; a removed series' entry goes
    /// with the next composition.
    /// </para>
    /// <para>
    /// <b>A hidden series builds nothing</b>, however its points change while hidden. Its entry is kept as it was,
    /// so showing it again with nothing changed reuses its geometry, and showing it after appends finds its
    /// generation moved on and builds it once, from the points as they are then.
    /// </para>
    /// </remarks>
    private SeriesFrame BuildSeries()
    {
        _counters.SeriesBuilds++;

        var plot = _tickPlotBounds;
        var series = Series;
        var count = series?.Count ?? 0;
        var placed = new List<PlacedSeries>(count);
        var previous = _seriesEntries;
        var entries = new Dictionary<LineSeries, SeriesEntry>(count);
        var previousPens = _seriesPens;
        var pens = new Dictionary<(Brush Brush, double Thickness), Pen>();

        if (series is not null && series.Count > 0
            && SeriesPlacement.TryCreateScale(plot, MinimumX, MaximumX, MinimumY, MaximumY, out var scale))
        {
            var fallback = SeriesStroke;

            for (var i = 0; i < series.Count; i++)
            {
                if (series[i] is not { } item)
                {
                    continue;
                }

                // The same series twice in the collection is one entry, made at its first occurrence.
                var known = entries.TryGetValue(item, out var entry) || previous.TryGetValue(item, out entry);

                if (!item.IsVisible)
                {
                    if (known)
                    {
                        entries[item] = entry;

                        // Its pen is carried too while its style is unchanged, so showing it again makes nothing.
                        if (entry.StyleGeneration == item.StyleGeneration)
                        {
                            Carry(item.Stroke ?? fallback, item.StrokeThickness, previousPens, pens);
                        }
                    }

                    continue;
                }

                var restyled = !known || entry.StyleGeneration != item.StyleGeneration;
                var pen = SeriesPen(item.Stroke ?? fallback, item.StrokeThickness, restyled ? null : previousPens, pens);
                var geometry = SeriesGeometry.None;

                // No pen, nothing to see: the geometry is not built for a series whose style draws nothing.
                if (pen is not null)
                {
                    if (entry.Geometry is { } kept && entry.Epoch == _seriesEpoch && entry.GeometryGeneration == item.GeometryGeneration)
                    {
                        geometry = kept;
                        _cacheCounters.SeriesGeometryHits++;
                    }
                    else
                    {
                        var points = item.Points;

                        geometry = PlaceSeries(item, points, scale, ClipToPlotArea);
                        entry = entry with { Epoch = _seriesEpoch, GeometryGeneration = item.GeometryGeneration, Geometry = geometry };
                        _cacheCounters.SeriesGeometryBuilds++;
                        _processingCounters.SeriesPointsPlaced += points.Count;
                    }
                }

                entries[item] = entry with { StyleGeneration = item.StyleGeneration };
                placed.Add(new PlacedSeries(i, geometry, pen));
            }
        }

        _seriesEntries = entries;
        _seriesPens = pens;

        return new SeriesFrame(placed, plot, ClipToPlotArea);
    }

    /// <summary>One series' geometry for a scale, for the main plot or the lens: every point, or reduced if it asked to be.</summary>
    /// <param name="item">The series.</param>
    /// <param name="points">Its points.</param>
    /// <param name="scale">The scale to place them by.</param>
    /// <param name="clipped">Whether what is drawn is clipped to the scale's rectangle.</param>
    private SeriesGeometry PlaceSeries(LineSeries item, IReadOnlyList<ChartPoint> points, in SeriesScale scale, bool clipped)
        => item.IsRenderReductionEnabled
            ? SeriesPlacement.BuildReduced(points, scale, VisualTreeHelper.GetDpi(this).PixelsPerDip, clipped, item.StrokeThickness)
            : SeriesPlacement.Build(points, scale);

    /// <summary>Whether any series asks for reduced geometry, which a change of DPI or clip then has to build again.</summary>
    private bool HasReducedSeries()
    {
        if (Series is { } series)
        {
            foreach (var item in series)
            {
                if (item is { IsRenderReductionEnabled: true })
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The pen for one series style, shared by every series with the same brush and thickness.
    /// </summary>
    /// <remarks>
    /// A frozen snapshot of the caller's brush, like every pen here but the tick pen, with round joins: a
    /// polyline of many short segments turns sharply, and a mitred corner would throw a spike well past the
    /// point it turns at.
    /// </remarks>
    private Pen? SeriesPen(
        Brush? brush,
        double thickness,
        Dictionary<(Brush Brush, double Thickness), Pen>? previous,
        Dictionary<(Brush Brush, double Thickness), Pen> current)
    {
        if (brush is null || !(thickness > 0))
        {
            _cacheCounters.SeriesStyleHits++;

            return null;
        }

        var key = (brush, thickness);

        // A null previous is a series whose style changed since the last composition: see ReferencePen.
        if (current.TryGetValue(key, out var pen) || (previous is not null && previous.TryGetValue(key, out pen)))
        {
            _cacheCounters.SeriesStyleHits++;
        }
        else
        {
            pen = FrozenPen(brush, thickness, PenLineJoin.Round)!;
            _cacheCounters.SeriesStyleBuilds++;
        }

        current[key] = pen;

        return pen;
    }

    /// <summary>
    /// Carries one item's pen from the last composition into this one without drawing it: an item not drawn now —
    /// hidden, or out of its range — whose style has not changed since it was last stroked.
    /// </summary>
    /// <remarks>
    /// Its unchanged <c>StyleGeneration</c> is what makes the pen still good: a change inside its brush would have
    /// moved it on. A pen the last composition did not have is not made here; it is made when the item is drawn.
    /// </remarks>
    private static void Carry(
        Brush? brush,
        double thickness,
        Dictionary<(Brush Brush, double Thickness), Pen> previous,
        Dictionary<(Brush Brush, double Thickness), Pen> current)
    {
        if (brush is not null && previous.TryGetValue((brush, thickness), out var pen))
        {
            current.TryAdd((brush, thickness), pen);
        }
    }

    /// <summary>
    /// A frozen pen over a brush, without freezing the caller's brush.
    /// </summary>
    /// <remarks>
    /// Freezing a <see cref="Pen"/> freezes the brush inside it, in place. That is harmless for a brush
    /// that is already frozen — every brush from a style or a resource is — but a caller's mutable brush
    /// would become read-only behind their back, and the next change they made to it would throw. So an
    /// unfrozen brush is drawn through a frozen snapshot instead; when the caller changes the brush, the
    /// change arrives as a sub-property change, the cached pen is dropped and a new snapshot taken. Solid,
    /// with flat caps, so a line ends exactly at the plot edge. No pen at all for a null brush or a zero
    /// thickness, so neither records draw commands that paint nothing.
    /// <para>
    /// A brush that cannot be frozen at all — a visual brush over a live element, say — is drawn live
    /// through an unfrozen pen, which is what it would need anyway.
    /// </para>
    /// </remarks>
    private static Pen? FrozenPen(Brush? brush, double thickness, PenLineJoin join = PenLineJoin.Miter)
    {
        if (brush is null || !(thickness > 0))
        {
            return null;
        }

        if (!brush.IsFrozen && !brush.CanFreeze)
        {
            return new Pen(brush, thickness) { LineJoin = join };
        }

        var stroke = brush.IsFrozen ? brush : (Brush)brush.GetCurrentValueAsFrozen();
        var pen = new Pen(stroke, thickness) { LineJoin = join };

        pen.Freeze();

        return pen;
    }

    /// <summary>Returns the horizontal ticks, regenerating them only if the range changed.</summary>
    private IReadOnlyList<Tick> HorizontalTicks()
    {
        var key = (MinimumX, MaximumX, XAxisTargetTickCount);

        if (_horizontalTicks is null || _horizontalKey != key)
        {
            _horizontalKey = key;
            _horizontalTicks = LinearTickGenerator.Generate(key.Item1, key.Item2, key.Item3);
        }

        return _horizontalTicks;
    }

    /// <summary>Returns the vertical ticks, regenerating them only if the range changed.</summary>
    private IReadOnlyList<Tick> VerticalTicks()
    {
        var key = (MinimumY, MaximumY, YAxisTargetTickCount);

        if (_verticalTicks is null || _verticalKey != key)
        {
            _verticalKey = key;
            _verticalTicks = LinearTickGenerator.Generate(key.Item1, key.Item2, key.Item3);
        }

        return _verticalTicks;
    }

    /// <summary>
    /// Builds the pen tick marks are stroked with, from <see cref="Control.Foreground"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reuses an existing brush property rather than adding a tick-specific one, so ticks are
    /// stylable without a tick-specific styling API. Rebuilt only when the brush is
    /// replaced, or when <see cref="OnForegroundChanged"/> drops it.
    /// </para>
    /// <para>
    /// <b>A frozen brush</b> — every brush from a style or a resource — is drawn through a frozen pen,
    /// which is never re-validated on render. <b>A mutable brush</b> is the caller's, and is drawn
    /// <em>live</em>: through an unfrozen pen over that very brush, which is never frozen. Not through a
    /// snapshot, as the grid, origin and reference pens are, because <see cref="Control.Foreground"/> is
    /// inherited: a change inside a brush set on an ancestor is reported to that ancestor, not to this
    /// control, so a snapshot would silently go stale. A live pen needs no report — WPF's renderer
    /// follows a change inside a brush used in a drawing, exactly as it does for the labels, which have
    /// always drawn this brush live through their text.
    /// </para>
    /// <para>
    /// The pen is not built directly over <see cref="Control.Foreground"/> and frozen, which
    /// would freeze a caller's mutable brush in place.
    /// </para>
    /// </remarks>
    private Pen? TickPen()
    {
        var brush = Foreground;

        if (brush is null)
        {
            _cacheCounters.TickStyleHits++;

            return null;
        }

        if (_tickPen is not null && ReferenceEquals(_tickPenBrush, brush))
        {
            _cacheCounters.TickStyleHits++;

            return _tickPen;
        }

        var pen = brush.IsFrozen ? FrozenPen(brush, 1) : new Pen(brush, 1);

        _tickPenBrush = brush;
        _tickPen = pen;
        _cacheCounters.TickStyleBuilds++;

        return pen;
    }

    /// <summary>
    /// One origin line placed against one plot and its axis's range: kept until those or its visibility change.
    /// </summary>
    /// <param name="Line">The line, or null when it is not drawn — hidden, or 0 outside the range.</param>
    /// <remarks>
    /// A class so that "not placed" (no part) and "placed, and not drawn" (a part holding null) stay two different
    /// things. One per origin: y = 0 belongs to the vertical range, x = 0 to the horizontal.
    /// </remarks>
    private sealed record OriginPart(PlotLine? Line);

    /// <summary>What the cache holds for one reference line.</summary>
    /// <param name="Epoch">The plot-and-range epoch, of the line's own axis, the line was placed in.</param>
    /// <param name="Axis">The axis the line was placed on, whose epoch <paramref name="Epoch"/> is.</param>
    /// <param name="GeometryGeneration">The line's <see cref="ChartReferenceLine.GeometryGeneration"/> when placed.</param>
    /// <param name="HasLine">Whether it is drawn at all: visible and inside its range.</param>
    /// <param name="Line">Where it is drawn, when it is.</param>
    /// <param name="StyleGeneration">The line's <see cref="ChartReferenceLine.StyleGeneration"/> when last stroked.</param>
    private readonly record struct ReferenceEntry(int Epoch, ReferenceLineAxis Axis, int GeometryGeneration, bool HasLine, PlotLine Line, int StyleGeneration);

    /// <summary>What the cache holds for one series.</summary>
    /// <param name="Epoch">The plot-and-range epoch the geometry was built in.</param>
    /// <param name="GeometryGeneration">The series' <see cref="LineSeries.GeometryGeneration"/> when built.</param>
    /// <param name="Geometry">The geometry, or null when none has been built.</param>
    /// <param name="StyleGeneration">The series' <see cref="LineSeries.StyleGeneration"/> when last stroked.</param>
    private readonly record struct SeriesEntry(int Epoch, int GeometryGeneration, SeriesGeometry? Geometry, int StyleGeneration);
}
