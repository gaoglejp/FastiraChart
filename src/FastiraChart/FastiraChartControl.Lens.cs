using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FastiraChart.Axes;
using FastiraChart.Gridlines;
using FastiraChart.Lens;
using FastiraChart.ReferenceLines;
using FastiraChart.Rendering;
using FastiraChart.Series;

namespace FastiraChart;

// <summary>
// The lens's composition: where it is and the chart content inside it.
// </summary>
// <remarks>
// <para>
// <b>The same chart through another scale.</b> Every coordinate inside the lens comes from the code that places the main
// chart — <see cref="LinearTickGenerator"/>, <see cref="MajorGridPlacement"/>, <see cref="ReferenceLinePlacement"/>,
// <see cref="SeriesPlacement"/>, <see cref="AxisTickPlacement"/>, <see cref="AxisLabelGeometry"/> — handed the lens
// rectangle in place of the plot and the effective lens source in place of the main horizontal range. The lens rectangle
// has the plot's very top and height, so every Y it produces is the main chart's Y, the same double; and a tick, a grid
// line, a reference line, the origin and a series point at one X value inside the lens are one calculation, as they are
// in the main chart.
// </para>
// <para>
// <b>The same styles, the very objects.</b> Pens are taken from the main layers' current frames — the grid pen, each
// reference line's and each series' pen, the origin pen, the tick pen — so the lens makes no pen, freezes nothing of the
// caller's, and moves no main cache count. The labels are measured by the main label pipeline: the same formatter,
// culture, typeface, size and <see cref="System.Windows.Controls.Control.Foreground"/>, the same rotation, anchor and
// gap.
// </para>
// <para>
// <b>Parts, as in the main layers.</b> The composition is put together from parts each kept until something it is drawn from changes:
// the lens's X ticks; the X part (vertical grid lines, x = 0, tick marks), valid for one placement of the lens; the Y part
// (horizontal grid lines, y = 0), valid while the lens's left and right edges and the vertical range stand; one entry per
// reference line and per series, kept by its generation; the label text, its placements and its brush. Two epochs say
// when the lens's X mapping and its Y mapping last moved.
// </para>
// </remarks>
public partial class FastiraChartControl
{
    /// <summary>How many X ticks the lens axis aims for across its own width.</summary>
    /// <remarks>
    /// <para>
    /// The control's default target, and deliberately not <see cref="XAxisTargetTickCount"/>. The lens is a detail
    /// view: its ticks are generated over the narrow lens source, so at the main axis's target a dense main axis would give
    /// an even denser lens, and the region the lens exists to make readable would not be. At a fixed, moderate target the
    /// lens step is finer than the main step whenever the source is narrower than the main range's own tick span — main
    /// 0..100 every 20, lens 40..60 every 5 — and a crowded main axis — target 100, every 1 — reads in the lens at about six
    /// labels across it.
    /// </para>
    /// <para>
    /// Internal and fixed. Across lens widths, fonts, rotations, anchors and label lengths it keeps
    /// at least three non-overlapping labels in every lens of ordinary width with ordinary labels; where long labels
    /// overlap, they overlap as the main axis's do, and rotation is the remedy there as it is there. A target adapting to
    /// the label size would be the automatic thinning the axes deliberately do not do, and a public setting is not needed
    /// by anything measured.
    /// </para>
    /// </remarks>
    internal const int LensTargetTickCount = DefaultTargetTickCount;

    // The lens's X epoch moves whenever the lens is placed again (its rectangle or its source may have moved); its Y epoch
    // when the plot or the vertical range moves, or the lens's left or right edge does (Y lines span the lens's width).
    private int _lensEpochX;
    private int _lensEpochY;
    private Rect _lensEpochBounds;
    private bool _lensShown;

    private IReadOnlyList<Tick>? _lensTicks;
    private (double Minimum, double Maximum, int Target) _lensTickKey;
    private LensXPart? _lensX;
    private LensYPart? _lensY;
    private Dictionary<ChartReferenceLine, LensReferenceEntry> _lensReferences = [];
    private Dictionary<LineSeries, LensSeriesEntry> _lensSeries = [];
    private IReadOnlyList<Tick>? _lensLabelTicks;
    private IReadOnlyList<FormattedText> _lensLabelTexts = [];
    private LabelMetrics[] _lensLabelMetrics = [];
    private bool _lensLabelTextsDirty = true;
    private bool _lensLabelStyleStale;
    private List<PlacedLabel>? _lensLabelPlacements;
    private int _lensLabelEpoch = -1;

    /// <summary>How many lens content entries the control holds: references, series, label texts.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation: the memory monitor holds the reference and
    /// series entries to the collections' sizes and the text to the lens's tick count, so a lens cache that kept entries
    /// of removed items, or a generation per source visited, is caught.
    /// </remarks>
    internal (int References, int Series, int Labels) LensCacheEntries
        => (_lensReferences.Count, _lensSeries.Count, _lensLabelTexts.Count);

    /// <summary>Drops every lens content cache, so the next composition makes every part again.</summary>
    /// <remarks>Used where every cache is dropped: a new template, and internal validation's stale-cache monitor.</remarks>
    private void DropLensContent()
    {
        _lensPart = null;
        _lensTicks = null;
        _lensX = null;
        _lensY = null;
        _lensReferences = [];
        _lensSeries = [];
        _lensLabelTextsDirty = true;
        _lensLabelPlacements = null;
    }

    /// <summary>Drops the lens's measured label text: a font, the DPI or a template changed.</summary>
    private void InvalidateLensLabels()
    {
        _lensLabelTextsDirty = true;
        _lensLabelPlacements = null;
    }

    /// <summary>
    /// Composes the lens against the current plot and the main ranges, for the lens layer to draw.
    /// </summary>
    /// <remarks>
    /// Called by the layer when it renders a stale frame, once per frame however many changes came before it. Placed
    /// against <c>_tickPlotBounds</c> — the rectangle every main layer is placed against. An inactive lens — hidden, with no
    /// source inside the main range, or no room in the plot — composes no content at all, so nothing stale survives it,
    /// and builds nothing while it stays so. It lets go of its reference and series entries too: they would hold removed
    /// items and whole series geometries alive for as long as it stays inactive, and the placement that makes it active
    /// again is a new X epoch, in which no series geometry could be reused anyway.
    /// </remarks>
    private LensComposition BuildLens()
    {
        _lensCounters.Builds++;

        var plot = _tickPlotBounds;

        if (_lensPart is not { } part)
        {
            var active = LensMapping.TryPlace(LensState, plot, MinimumX, MaximumX, out var placement);

            part = new LensPart(active, placement);
            _lensPart = part;
            _lensCounters.PlacementBuilds++;

            // A new placement is a new X mapping; new edges are a new width for every Y line.
            _lensEpochX++;

            if (active && (placement.Bounds.Left != _lensEpochBounds.Left || placement.Bounds.Right != _lensEpochBounds.Right))
            {
                _lensEpochBounds = placement.Bounds;
                _lensEpochY++;
            }
        }
        else
        {
            _lensCounters.PlacementHits++;
        }

        if (!part.Active || !LinearAxisMapping.TryGetInverseRange(MinimumX, MaximumX, out var inverse))
        {
            _lensShown = false;
            _lensReferences.Clear();
            _lensSeries.Clear();

            return new LensComposition(LensFrame.Inactive(plot), LensContent.Empty);
        }

        _lensShown = true;

        var hasScale = LensMapping.TryCreateScale(part.Placement, MinimumY, MaximumY, out var scale);

        // The source's ends in the main chart's X, by the main chart's expressions: both lie inside the main range, which
        // the source was intersected with.
        var left = AxisTickPlacement.HorizontalCoordinate(plot, LinearAxisMapping.Normalize(part.Placement.SourceMinimum, MinimumX, inverse));
        var right = AxisTickPlacement.HorizontalCoordinate(plot, LinearAxisMapping.Normalize(part.Placement.SourceMaximum, MinimumX, inverse));
        var frame = new LensFrame(true, plot, part.Placement, scale, hasScale, left, right);

        return new LensComposition(frame, ComposeLensContent(part.Placement, scale, hasScale));
    }

    /// <summary>
    /// Puts the lens content together from its parts, making again only what was dropped or moved.
    /// </summary>
    private LensContent ComposeLensContent(LensPlacement placement, SeriesScale scale, bool hasScale)
    {
        var bounds = placement.Bounds;
        var ticks = LensTicks(placement);

        // The X part: the lens's vertical grid lines, x = 0 and the tick marks, all at the lens X of their value.
        if (_lensX is not { } x || x.Epoch != _lensEpochX || !ReferenceEquals(x.Ticks, ticks))
        {
            var vertical = new List<GridLine>(ticks.Count);

            if (IsVerticalMajorGridVisible)
            {
                MajorGridPlacement.AppendVertical(vertical, bounds, ticks);
            }

            PlotLine? origin = IsVerticalOriginLineVisible && ReferenceLinePlacement.TryVertical(bounds, placement.SourceMinimum, placement.SourceMaximum, 0, out var x0) ? x0 : null;

            x = new LensXPart(_lensEpochX, ticks, vertical, origin, PlaceLensTickMarks(bounds, ticks));
            _lensX = x;
            _lensCounters.XBuilds++;
        }
        else
        {
            _lensCounters.XHits++;
        }

        // The Y part: the main vertical ticks and y = 0 at the main chart's Y — the lens rectangle has the plot's top and
        // height, so these are the very doubles the main grid and origin are placed at — across the lens's width.
        if (_lensY is not { } y || y.Epoch != _lensEpochY)
        {
            var horizontal = new List<GridLine>();

            if (IsHorizontalMajorGridVisible)
            {
                MajorGridPlacement.AppendHorizontal(horizontal, bounds, VerticalTicks());
            }

            PlotLine? origin = IsHorizontalOriginLineVisible && ReferenceLinePlacement.TryHorizontal(bounds, MinimumY, MaximumY, 0, out var y0) ? y0 : null;

            y = new LensYPart(_lensEpochY, horizontal, origin);
            _lensY = y;
            _lensCounters.YBuilds++;
        }
        else
        {
            _lensCounters.YHits++;
        }

        // No scale, no series drawn: what gives the lens a scale again moves an epoch, so nothing here could be reused.
        if (!hasScale)
        {
            _lensSeries.Clear();
        }

        return new LensContent(
            y.Grid,
            x.Grid,
            _gridLayer?.Pen,
            ComposeLensReferences(placement),
            y.Origin,
            x.Origin,
            _referenceLayer?.OriginPen,
            hasScale ? ComposeLensSeries(scale) : [],
            ticks,
            x.TickMarks,
            IsXAxisVisible ? _tickLayer?.Pen : null,
            ComposeLensLabels(bounds, ticks));
    }

    /// <summary>The lens's own X ticks: the main tick generator over the effective source, at the lens target.</summary>
    /// <remarks>
    /// Generated again only when the effective source changes; see <see cref="LensTargetTickCount"/>.
    /// </remarks>
    private IReadOnlyList<Tick> LensTicks(LensPlacement placement)
    {
        var key = (placement.SourceMinimum, placement.SourceMaximum, LensTargetTickCount);

        if (_lensTicks is null || _lensTickKey != key)
        {
            _lensTickKey = key;
            _lensTicks = LinearTickGenerator.Generate(key.Item1, key.Item2, key.Item3);
            _lensCounters.TickGenerations++;
        }

        return _lensTicks;
    }

    /// <summary>One mark per lens tick, from the lens's bottom or top edge inward, the main tick length.</summary>
    /// <remarks>
    /// The lens's X axis is the main horizontal axis's: on the edge <see cref="XAxisPosition"/> names, drawn only
    /// while <see cref="IsXAxisVisible"/> is. Inward, because the lens is inside the plot and draws nothing outside
    /// itself. At the X <see cref="AxisTickPlacement.HorizontalCoordinate(Rect, Tick)"/> gives — the grid line's.
    /// </remarks>
    private List<TickMark> PlaceLensTickMarks(Rect bounds, IReadOnlyList<Tick> ticks)
    {
        var marks = new List<TickMark>(ticks.Count);

        if (!IsXAxisVisible || ticks.Count == 0)
        {
            return marks;
        }

        var bottom = XAxisPosition == Axes.HorizontalAxisPosition.Bottom;
        var edge = bottom ? bounds.Bottom : bounds.Top;
        var length = Math.Min(MajorTickLength, bounds.Height);
        var tip = bottom ? edge - length : edge + length;

        foreach (var tick in ticks)
        {
            var at = AxisTickPlacement.HorizontalCoordinate(bounds, tick);

            marks.Add(new TickMark(new Point(at, edge), new Point(at, tip)));
        }

        return marks;
    }

    /// <summary>
    /// Every reference line inside the lens, in collection order: X values through the lens's X mapping, Y values at the
    /// main chart's Y across the lens's width; each line placed again only when its own geometry or the mapping it is drawn
    /// by moved on.
    /// </summary>
    /// <remarks>
    /// An X value outside the effective source has no line in the lens: not clamped onto the lens's edge, exactly as the
    /// main chart treats a value outside its range. The pen is the one the main chart strokes the line with; a line the
    /// main chart does not draw — hidden, or with no pen — is not drawn here either.
    /// </remarks>
    private List<LensReference> ComposeLensReferences(LensPlacement placement)
    {
        var lines = ReferenceLines;
        var placed = new List<LensReference>();
        var previous = _lensReferences;
        var entries = new Dictionary<ChartReferenceLine, LensReferenceEntry>(lines?.Count ?? 0);

        if (lines is null || lines.Count == 0 || _referenceLayer is not { } layer)
        {
            _lensReferences = entries;

            return placed;
        }

        var pens = new Dictionary<int, Pen?>();

        foreach (var main in layer.References)
        {
            pens[main.Index] = main.Pen;
        }

        var bounds = placement.Bounds;

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i] is not { } line)
            {
                continue;
            }

            var onX = line.Axis == ReferenceLineAxis.X;
            var epoch = onX ? _lensEpochX : _lensEpochY;
            var known = entries.TryGetValue(line, out var entry) || previous.TryGetValue(line, out entry);

            if (known && entry.Epoch == epoch && entry.Axis == line.Axis && entry.GeometryGeneration == line.GeometryGeneration)
            {
                _lensCounters.ReferenceHits++;
            }
            else
            {
                var geometry = default(PlotLine);
                var hasLine = line.IsVisible
                    && (onX
                        ? ReferenceLinePlacement.TryVertical(bounds, placement.SourceMinimum, placement.SourceMaximum, line.Value, out geometry)
                        : ReferenceLinePlacement.TryHorizontal(bounds, MinimumY, MaximumY, line.Value, out geometry));

                entry = new LensReferenceEntry(epoch, line.Axis, line.GeometryGeneration, hasLine, geometry);

                if (onX)
                {
                    _lensCounters.ReferenceXBuilds++;
                }
                else
                {
                    _lensCounters.ReferenceYBuilds++;
                }
            }

            entries[line] = entry;

            if (entry.HasLine && pens.TryGetValue(i, out var pen) && pen is not null)
            {
                placed.Add(new LensReference(i, line.Axis, entry.Line, pen));
            }
        }

        _lensReferences = entries;

        return placed;
    }

    /// <summary>
    /// Every series the main chart draws, in collection order, placed by the lens scale; each built again only when its
    /// points moved on or the lens's X or Y mapping did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SeriesPlacement.Build"/> itself (or <see cref="SeriesPlacement.BuildReduced"/>, for a series that asked
    /// for it), handed the lens scale: every finite point is placed — including one
    /// outside the lens source, so a segment between a point left of the source and one right of it crosses the lens and
    /// is drawn, the clip deciding what shows — non-finite values are gaps the line never crosses, and the render guard cuts
    /// a segment reaching too far past the lens rectangle where it leaves the guard band, so a large offset or a high
    /// magnification never loses a crossing line.
    /// </para>
    /// <para>
    /// The series' pen is the main chart's for that series; a hidden series, or one whose style draws nothing, is not drawn
    /// and builds nothing. Its entry is kept, so showing it again reuses its lens geometry when nothing moved.
    /// </para>
    /// </remarks>
    private List<PlacedSeries> ComposeLensSeries(SeriesScale scale)
    {
        var series = Series;
        var placed = new List<PlacedSeries>();
        var previous = _lensSeries;
        var entries = new Dictionary<LineSeries, LensSeriesEntry>(series?.Count ?? 0);

        if (series is null || series.Count == 0 || _seriesLayer is not { } layer)
        {
            _lensSeries = entries;

            return placed;
        }

        foreach (var main in layer.Placed)
        {
            if (main.Pen is not { } pen || main.Index >= series.Count || series[main.Index] is not { } item)
            {
                continue;
            }

            var known = entries.TryGetValue(item, out var entry) || previous.TryGetValue(item, out entry);
            SeriesGeometry geometry;

            if (known && entry.Geometry is { } kept && entry.EpochX == _lensEpochX && entry.EpochY == _lensEpochY && entry.GeometryGeneration == item.GeometryGeneration)
            {
                geometry = kept;
                _lensCounters.SeriesHits++;
            }
            else
            {
                var points = item.Points;

                // Whatever the main clip is set to: everything in the lens is clipped to the lens.
                geometry = PlaceSeries(item, points, scale, clipped: true);
                entry = new LensSeriesEntry(_lensEpochX, _lensEpochY, item.GeometryGeneration, geometry);
                _lensCounters.SeriesBuilds++;
                _processingCounters.LensSeriesPointsPlaced += points.Count;
            }

            entries[item] = entry;
            placed.Add(new PlacedSeries(main.Index, geometry, pen));
        }

        // Hidden series keep their entry, as the main chart keeps theirs.
        foreach (var item in series)
        {
            if (item is not null && !entries.ContainsKey(item) && previous.TryGetValue(item, out var entry))
            {
                entries[item] = entry;
            }
        }

        _lensSeries = entries;

        return placed;
    }

    /// <summary>
    /// One label per lens tick, measured by the main label pipeline and placed inside the lens beyond its tick mark.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The main labels' meaning, moved inward.</b> Each label is placed by <see cref="AxisLabelGeometry.Place"/> with the
    /// main horizontal axis's side, anchor and rotation, so the shape a rotation and an anchor give a label is exactly the
    /// main axis's. It is then translated across the axis so that, where a main label's inward edge rests one gap beyond
    /// its tick, a lens label's edge nearest its tick rests one gap beyond the lens tick — inside the lens. Nothing is
    /// rotated, re-anchored, shortened, thinned or dropped; labels may overlap, as main labels may.
    /// </para>
    /// <para>
    /// <b>No effect on the layout.</b> The lens labels are not measured into <see cref="RequiredAxisSpace"/>; they are
    /// clipped to the lens.
    /// </para>
    /// </remarks>
    private List<PlacedLabel> ComposeLensLabels(Rect bounds, IReadOnlyList<Tick> ticks)
    {
        if (_lensLabelTextsDirty || !ReferenceEquals(_lensLabelTicks, ticks))
        {
            var brush = Foreground ?? Brushes.Transparent;
            var style = new LabelTextStyle(new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, VisualTreeHelper.GetDpi(this).PixelsPerDip, brush, CultureInfo.CurrentCulture);

            // The lens's last texts, as the main axes': only in the very style, with the very brush they carry.
            _lensLabelTexts = BuildLabelTexts(ticks, style, _lensLabelStyle == style ? _lensLabelTexts : null);
            _lensLabelStyle = style;
            _lensLabelMetrics = [.. _lensLabelTexts.Select(AxisLabelMeasurement.MetricsOf)];
            _lensLabelTicks = ticks;
            _lensLabelTextsDirty = false;
            _lensLabelStyleStale = false;
            _lensLabelPlacements = null;
            _lensCounters.LabelMeasurements++;
        }
        else
        {
            _lensCounters.LabelMeasurementHits++;
        }

        if (_lensLabelStyleStale)
        {
            var brush = Foreground ?? Brushes.Transparent;

            foreach (var text in _lensLabelTexts)
            {
                text.SetForegroundBrush(brush);
            }

            _lensLabelStyle = _lensLabelStyle is { } lens ? lens with { Brush = brush } : null;
            _lensLabelStyleStale = false;
            _lensCounters.LabelStyleBuilds++;
        }

        if (_lensLabelPlacements is { } kept && _lensLabelEpoch == _lensEpochX)
        {
            _lensCounters.LabelPlacementHits++;

            return kept;
        }

        var placed = new List<PlacedLabel>(_lensLabelTexts.Count);

        if (IsXAxisVisible)
        {
            var side = XAxisPosition.ToSide();
            var bottom = side == AxisSide.Bottom;
            var reach = Math.Min(MajorTickLength, bounds.Height) + NonNegativeGap();
            var anchor = XAxisLabelAnchor;
            var rotation = XAxisLabelRotation;

            for (var i = 0; i < ticks.Count && i < _lensLabelTexts.Count; i++)
            {
                var at = AxisTickPlacement.HorizontalCoordinate(bounds, ticks[i]);
                var extent = AxisLabelGeometry.Place(_lensLabelMetrics[i], anchor, rotation, side, new Point(at, 0)).OutwardExtent;

                // On the bottom edge a main label reaches down from its anchor; the lens label is lifted by the tick, the
                // gap and its own reach, so its lower edge rests one gap above the lens tick. On the top edge, mirrored.
                var y = bottom ? bounds.Bottom - reach - extent : bounds.Top + reach + extent;

                placed.Add(Placed(_lensLabelTexts[i], AxisLabelGeometry.Place(_lensLabelMetrics[i], anchor, rotation, side, new Point(at, y))));
            }
        }

        _lensLabelPlacements = placed;
        _lensLabelEpoch = _lensEpochX;
        _lensCounters.LabelPlacementBuilds++;

        return placed;
    }

    /// <summary>
    /// The lens's horizontal mapping placed against one plot and one main horizontal range, or the finding that the lens
    /// has none there.
    /// </summary>
    /// <param name="Active">Whether the lens has geometry: shown, a source inside the main range, room in the plot.</param>
    /// <param name="Placement">The rectangle and source, when active.</param>
    /// <remarks>A class so "not placed" (no part) and "placed, and inactive" stay two different things.</remarks>
    private sealed record LensPart(bool Active, LensPlacement Placement);

    /// <summary>The lens's X part: what is drawn from its X mapping, for one placement and one tick sequence.</summary>
    private sealed record LensXPart(int Epoch, IReadOnlyList<Tick> Ticks, IReadOnlyList<GridLine> Grid, PlotLine? Origin, IReadOnlyList<TickMark> TickMarks);

    /// <summary>The lens's Y part: what is drawn from the main Y mapping across the lens, for one Y epoch.</summary>
    private sealed record LensYPart(int Epoch, IReadOnlyList<GridLine> Grid, PlotLine? Origin);

    /// <summary>What the lens keeps for one reference line.</summary>
    private readonly record struct LensReferenceEntry(int Epoch, ReferenceLineAxis Axis, int GeometryGeneration, bool HasLine, PlotLine Line);

    /// <summary>What the lens keeps for one series.</summary>
    private readonly record struct LensSeriesEntry(int EpochX, int EpochY, int GeometryGeneration, SeriesGeometry? Geometry);
}
