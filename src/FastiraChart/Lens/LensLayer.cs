using System.Windows;
using System.Windows.Media;
using FastiraChart.Axes;
using FastiraChart.Gridlines;
using FastiraChart.ReferenceLines;
using FastiraChart.Rendering;
using FastiraChart.Series;

namespace FastiraChart.Lens;

/// <summary>
/// Where the lens is and how it maps: the placement part of a lens frame.
/// </summary>
/// <param name="IsActive">Whether the lens has geometry: shown, a source inside the main range, room in the plot.</param>
/// <param name="Plot">The main plot, which the source indicator is clipped to.</param>
/// <param name="Placement">The lens rectangle and source; meaningful only when active.</param>
/// <param name="Scale">
/// The scale a point is placed by inside the lens (<see cref="LensMapping.TryCreateScale"/>); meaningful only when
/// <paramref name="HasScale"/>. Every lens series point is placed by it.
/// </param>
/// <param name="HasScale">Whether the main vertical range has a scale, so the lens has one.</param>
/// <param name="SourceLeft">The main plot X of the source minimum: where the indicator marks the source's start.</param>
/// <param name="SourceRight">The main plot X of the source maximum.</param>
internal readonly record struct LensFrame(
    bool IsActive,
    Rect Plot,
    LensPlacement Placement,
    SeriesScale Scale,
    bool HasScale,
    double SourceLeft,
    double SourceRight)
{
    /// <summary>No lens: what an inactive lens, or a lens before its first build, draws.</summary>
    public static LensFrame Inactive(Rect plot) => new(false, plot, default, default, false, 0, 0);
}

/// <summary>One reference line as the lens draws it.</summary>
/// <param name="Index">Its position in <see cref="FastiraChartControl.ReferenceLines"/>.</param>
/// <param name="Axis">The axis it marks a value on.</param>
/// <param name="Line">
/// The line inside the lens: an X value as a vertical line at its lens X, the lens's full height; a Y value as a horizontal
/// line at the main chart's Y, the lens's width.
/// </param>
/// <param name="Pen">The pen the main chart strokes the line with — the very object.</param>
internal readonly record struct LensReference(int Index, ReferenceLineAxis Axis, PlotLine Line, Pen? Pen);

/// <summary>
/// The chart content drawn inside the lens, in drawing order.
/// </summary>
/// <param name="HorizontalGrid">Horizontal grid lines: the main vertical ticks, at the main chart's Y, across the lens.</param>
/// <param name="VerticalGrid">Vertical grid lines: the lens's own X ticks, at their lens X, the lens's full height.</param>
/// <param name="GridPen">The main grid pen.</param>
/// <param name="References">Every reference line inside the lens, in collection order.</param>
/// <param name="HorizontalOrigin">y = 0 across the lens, when the main vertical range holds 0 and it is shown.</param>
/// <param name="VerticalOrigin">x = 0 down the lens, when the effective lens source holds 0 and it is shown.</param>
/// <param name="OriginPen">The main origin pen.</param>
/// <param name="Series">Every visible series with a pen, in collection order, placed by the lens scale.</param>
/// <param name="Ticks">The lens's own X ticks, generated over the effective source.</param>
/// <param name="TickMarks">One mark per tick, from the lens's bottom or top edge inward.</param>
/// <param name="TickPen">The main tick pen.</param>
/// <param name="Labels">One label per tick, inside the lens beyond its mark.</param>
internal sealed record LensContent(
    IReadOnlyList<GridLine> HorizontalGrid,
    IReadOnlyList<GridLine> VerticalGrid,
    Pen? GridPen,
    IReadOnlyList<LensReference> References,
    PlotLine? HorizontalOrigin,
    PlotLine? VerticalOrigin,
    Pen? OriginPen,
    IReadOnlyList<PlacedSeries> Series,
    IReadOnlyList<Tick> Ticks,
    IReadOnlyList<TickMark> TickMarks,
    Pen? TickPen,
    IReadOnlyList<PlacedLabel> Labels)
{
    /// <summary>Nothing inside the lens: an inactive lens's content, so no stale content survives it.</summary>
    public static readonly LensContent Empty = new([], [], null, [], null, null, null, [], [], [], null, []);
}

/// <summary>Everything one render of the lens layer draws: where the lens is, and what is drawn in it.</summary>
/// <param name="Frame">The lens placement and mapping.</param>
/// <param name="Content">The chart content inside the lens; <see cref="LensContent.Empty"/> when the lens is inactive.</param>
internal readonly record struct LensComposition(LensFrame Frame, LensContent Content);

/// <summary>
/// Draws the lens over the plot: its rectangle, and the chart content inside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a second chart.</b> The lens is one more drawing layer of the same control, placed from the same plot and
/// ranges by the same mapping (<see cref="LensMapping"/>), drawing the same series, reference lines and grid settings
/// through the same placement code with the lens scale in place of the main one. No control, no copy of any data, no
/// range model of its own.
/// </para>
/// <para>
/// <b>What is drawn</b>. First, in the main chart's coordinates and clipped to the plot, a mark at each end
/// of the source (the <em>source indicator</em>). Then, clipped to the lens rectangle, bottom to top: the lens background;
/// the grid (horizontal lines, then vertical); the reference lines, then the origin lines; the series, in collection
/// order; the lens's X tick marks; their labels; the border. There is no lens Y axis: the lens's Y is the main chart's,
/// and the main axis already labels it.
/// </para>
/// <para>
/// <b>Clipped to the lens.</b> Everything drawn as lens content is under a clip of exactly the lens rectangle, whatever
/// <see cref="FastiraChartControl.ClipToPlotArea"/> says: that property is the main series' clip, and this is another.
/// </para>
/// <para>
/// <b>One element</b>, like every layer: no child, never hit-tested, nothing per point, tick or label, so the visual tree is
/// the same size whatever the lens shows. It sits above the series and the plot content and below the axes; that order
/// is the template's (<c>PART_LensHost</c>).
/// </para>
/// <para>
/// <b>Built when drawn</b> (the control's dirty model): a change to anything the lens draws from only marks this layer stale, and the
/// frame is composed when WPF next renders it, once however many changes came before.
/// </para>
/// </remarks>
internal sealed class LensLayer : DeferredLayer<LensComposition>
{
    /// <summary>The lens background colour.</summary>
    internal static readonly Color BackgroundColour = Color.FromRgb(0x12, 0x12, 0x18);

    /// <summary>Lens background: opaque, so the unmagnified chart beneath never reads as lens content.</summary>
    internal static readonly Brush Background = Frozen(BackgroundColour);

    /// <summary>The lens border and the source indicator.</summary>
    internal static readonly Color AccentColour = Color.FromRgb(0xE0, 0xB0, 0x40);

    /// <summary>Lens border thickness, in DIP, drawn inside the lens rectangle.</summary>
    internal const double BorderThickness = 1;

    private static readonly Pen BorderPen = FrozenPen(new SolidColorBrush(AccentColour), BorderThickness);
    private static readonly Pen IndicatorPen = FrozenPen(new SolidColorBrush(Color.FromArgb(0x90, 0xE0, 0xB0, 0x40)), 1);

    private RectangleGeometry? _lensClip;
    private Rect _lensClipBounds;

    /// <summary>
    /// Initializes a new instance of the <see cref="LensLayer"/> class.
    /// </summary>
    /// <param name="build">Builds the frame from the control as it is when called.</param>
    public LensLayer(Func<LensComposition> build)
        : base(build, new LensComposition(LensFrame.Inactive(default), LensContent.Empty))
    {
    }

    /// <summary>Where the lens is and how it maps, as drawn now.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation. Reading it brings the frame up to date first, exactly as the
    /// next render would. Internal: it is not a public lens API.
    /// </remarks>
    internal LensFrame Frame => Current.Frame;

    /// <summary>The content inside the lens, as drawn now.</summary>
    /// <remarks>Read-only instrumentation for internal validation, read as <see cref="Frame"/> is.</remarks>
    internal LensContent Content => Current.Content;

    /// <inheritdoc />
    protected override void Draw(DrawingContext drawingContext, LensComposition composition)
    {
        var frame = composition.Frame;

        if (!frame.IsActive)
        {
            return;
        }

        var content = composition.Content;
        var bounds = frame.Placement.Bounds;

        // The source indicator, in the main chart's X: where the magnified values are on the unmagnified plot. Main-chart
        // coordinates, so clipped to the plot, and drawn before the lens so the lens covers it where they meet.
        drawingContext.PushClip(ClipFor(frame.Plot));
        drawingContext.DrawLine(IndicatorPen, new Point(frame.SourceLeft, frame.Plot.Top), new Point(frame.SourceLeft, frame.Plot.Bottom));
        drawingContext.DrawLine(IndicatorPen, new Point(frame.SourceRight, frame.Plot.Top), new Point(frame.SourceRight, frame.Plot.Bottom));
        drawingContext.Pop();

        // Lens content: under the lens clip, all of it, in the main chart's own order.
        drawingContext.PushClip(LensClip(bounds));
        drawingContext.DrawRectangle(Background, null, bounds);

        if (content.GridPen is { } gridPen)
        {
            // Horizontal before vertical, as the main grid records them.
            foreach (var line in content.HorizontalGrid)
            {
                drawingContext.DrawLine(gridPen, line.Start, line.End);
            }

            foreach (var line in content.VerticalGrid)
            {
                drawingContext.DrawLine(gridPen, line.Start, line.End);
            }
        }

        foreach (var reference in content.References)
        {
            if (reference.Pen is { } pen)
            {
                drawingContext.DrawLine(pen, reference.Line.Start, reference.Line.End);
            }
        }

        // Origins over the reference lines, as in the main chart.
        if (content.OriginPen is { } originPen)
        {
            if (content.HorizontalOrigin is { } horizontal)
            {
                drawingContext.DrawLine(originPen, horizontal.Start, horizontal.End);
            }

            if (content.VerticalOrigin is { } vertical)
            {
                drawingContext.DrawLine(originPen, vertical.Start, vertical.End);
            }
        }

        foreach (var series in content.Series)
        {
            if (series.Pen is { } pen && series.Geometry.Geometry is { } geometry)
            {
                drawingContext.DrawGeometry(null, pen, geometry);
            }
        }

        if (content.TickPen is { } tickPen)
        {
            foreach (var mark in content.TickMarks)
            {
                drawingContext.DrawLine(tickPen, mark.Start, mark.End);
            }
        }

        foreach (var label in content.Labels)
        {
            var transform = label.Transform is { } frozen && frozen.Value == label.Placement.Transform
                ? frozen
                : new MatrixTransform(label.Placement.Transform);

            drawingContext.PushTransform(transform);
            drawingContext.DrawText(label.Text, new Point(0, 0));
            drawingContext.Pop();
        }

        // Inset by half the stroke, so the border lies inside the lens and the clip trims nothing of it; drawn last, over
        // everything inside the lens.
        var half = BorderThickness / 2;

        if (bounds.Width > BorderThickness && bounds.Height > BorderThickness)
        {
            drawingContext.DrawRectangle(null, BorderPen, new Rect(bounds.X + half, bounds.Y + half, bounds.Width - BorderThickness, bounds.Height - BorderThickness));
        }

        drawingContext.Pop();
    }

    /// <summary>A frozen clip over the lens rectangle, made again only when it moves.</summary>
    private Geometry LensClip(Rect bounds)
    {
        if (_lensClip is null || _lensClipBounds != bounds)
        {
            var clip = new RectangleGeometry(bounds);

            clip.Freeze();
            _lensClip = clip;
            _lensClipBounds = bounds;
        }

        return _lensClip;
    }

    private static Brush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);

        brush.Freeze();

        return brush;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);

        pen.Freeze();

        return pen;
    }
}
