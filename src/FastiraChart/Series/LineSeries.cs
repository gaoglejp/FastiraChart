using System.Windows;
using System.Windows.Media;

namespace FastiraChart.Series;

/// <summary>
/// A series of points joined, in order, by straight lines.
/// </summary>
/// <remarks>
/// <para>
/// Added to <see cref="FastiraChartControl.Series"/>. Every point is placed by the value-to-coordinate
/// mapping the ticks, the grid, the origin and the reference lines use, against the control's own X and Y
/// ranges, so a point at a tick's value lands exactly on that tick's grid line. Point 0 is
/// joined to point 1, point 1 to point 2, and so on, in the order given: points are never sorted, merged or
/// thinned out.
/// </para>
/// <para>
/// <b>A point that is not finite is a gap.</b> The line stops at the last finite point before it and starts
/// again at the first finite point after it; nothing joins across it. A run of finite points with a gap on
/// each side and only one point in it draws nothing, because a line needs two points.
/// </para>
/// <para>
/// <b>A point outside the range is kept.</b> It is placed where it lies, off the plot, so the segments to its
/// neighbours keep their true direction — a segment between a point left of the plot and one right of it
/// still crosses the plot. What shows is decided by <see cref="FastiraChartControl.ClipToPlotArea"/>.
/// </para>
/// <para>
/// <b>A <see cref="Freezable"/></b>, like <see cref="ReferenceLines.ChartReferenceLine"/>, which is what lets a
/// change reach the control without any event wiring: a change to any property — or inside a mutable
/// <see cref="Brush"/> it holds — notifies the collection, and the collection notifies the control, which
/// redraws. Removing the series from the collection is all it takes to detach it. Nothing about a series
/// takes any part in the layout.
/// </para>
/// <para>
/// <b>Four ways to change the points</b>, each one update and one redraw:
/// </para>
/// <list type="bullet">
/// <item><description>setting <see cref="Points"/> <b>replaces</b> them all;</description></item>
/// <item><description><see cref="Append"/> <b>adds one</b> after the last;</description></item>
/// <item><description><see cref="AppendRange"/> <b>adds a batch</b> after the last, in one redraw;</description></item>
/// <item><description><see cref="ClearPoints"/> <b>clears the data</b>, keeping the series and its style.</description></item>
/// </list>
/// <para>
/// They may be mixed freely; the series always draws exactly the points the sequence of calls describes.
/// </para>
/// <para>
/// <b>Points are a snapshot.</b> A list set on <see cref="Points"/> is read when the chart draws and is not watched for
/// later changes: adding to or changing a list that is already set is not noticed — set a new list, or use the methods
/// above. A list that implements <c>INotifyCollectionChanged</c> (an <c>ObservableCollection</c>) is treated the same.
/// </para>
/// <para>
/// <b>One thread.</b> Like every unfrozen <see cref="Freezable"/>, a series belongs to the thread that created
/// it — the UI thread — and every change must be made there; from another thread it throws
/// <see cref="InvalidOperationException"/>, as WPF does, rather than corrupting anything. A series the caller
/// has frozen cannot be changed at all.
/// </para>
/// <para>
/// <b>Caller-owned.</b> FastiraChart never freezes or modifies a series, the point lists it is given, or the brush it
/// holds.
/// </para>
/// </remarks>
public sealed class LineSeries : Freezable
{
    /// <summary>Identifies the <see cref="Points"/> dependency property.</summary>
    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(
            nameof(Points),
            typeof(IReadOnlyList<ChartPoint>),
            typeof(LineSeries),
            new PropertyMetadata(Array.Empty<ChartPoint>(), OnGeometryInputChanged, CoercePoints));

    /// <summary>Identifies the <see cref="IsVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsVisibleProperty =
        DependencyProperty.Register(
            nameof(IsVisible),
            typeof(bool),
            typeof(LineSeries),
            new PropertyMetadata(true));

    /// <summary>Identifies the <see cref="Stroke"/> dependency property.</summary>
    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke),
            typeof(Brush),
            typeof(LineSeries),
            new PropertyMetadata(null, OnStyleInputChanged));

    /// <summary>Identifies the <see cref="StrokeThickness"/> dependency property.</summary>
    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(LineSeries),
            new PropertyMetadata(DefaultStrokeThickness, OnStyleInputChanged),
            IsValidThickness);

    /// <summary>Initial stroke thickness of a series line, in DIP.</summary>
    /// <remarks>
    /// Twice a grid line and heavier than the origin's 1.5, so the data is the heaviest thing in the plot. Not
    /// rounded to a device pixel, for the reason the grid's thickness gives.
    /// </remarks>
    internal const double DefaultStrokeThickness = 2;

    /// <summary>The series' own points once it has been appended to; never the caller's list.</summary>
    /// <remarks>Not copied by <see cref="CreateInstanceCore"/>: a clone shares the window it was cloned with, and copies it into a buffer of its own on its first append.</remarks>
    private SeriesPointBuffer? _buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LineSeries"/> class, with no points.
    /// </summary>
    public LineSeries()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LineSeries"/> class over a snapshot of points.
    /// </summary>
    /// <param name="points">The points, in drawing order; null is taken as none.</param>
    public LineSeries(IReadOnlyList<ChartPoint>? points)
    {
        Points = points!;
    }

    /// <summary>
    /// Gets or sets the points, in the order they are joined. The default is an empty list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A snapshot, replaced to update.</b> The control redraws when this property is set to a list, and
    /// reads the list it was given each time it draws. It does not watch the list: adding to, removing from
    /// or overwriting an element of a list that is already set is not noticed, and what is drawn may then be
    /// the old content or the new, depending on when the next redraw happens to come. To change the data, set
    /// a new list. A list that implements <c>INotifyCollectionChanged</c> is not treated any differently.
    /// </para>
    /// <para>
    /// The list is the caller's and is never modified or copied. An array of <see cref="ChartPoint"/> is the
    /// natural snapshot. Null is accepted and read back as an empty list.
    /// </para>
    /// <para>
    /// <b>Setting this property is the replace operation</b>: every point is replaced by the new list's, and
    /// nothing of the old points is kept or merged. To add points use <see cref="Append"/> or
    /// <see cref="AppendRange"/>; to remove them all use <see cref="ClearPoints"/>.
    /// </para>
    /// <para>
    /// <b>After an append or a clear, this property reads back the series' own list</b>, not the caller's: a
    /// read-only list of the current points, of no specified type. It holds those points until the next
    /// <see cref="Append"/>, <see cref="AppendRange"/> or <see cref="ClearPoints"/>. Whether a list read earlier
    /// still holds the same points after that is not specified, so copy it — for example with
    /// <c>ToArray()</c> — to keep it.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ChartPoint> Points
    {
        get => (IReadOnlyList<ChartPoint>)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>Gets or sets whether the series is drawn. The default is true.</summary>
    /// <remarks>
    /// Drawing only: a hidden series keeps its place and its points, and changes no layout. Shown again, it
    /// draws exactly the geometry it would have drawn had it never been hidden.
    /// </remarks>
    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush the line is stroked with. The default is null, which uses the control's
    /// <see cref="FastiraChartControl.SeriesStroke"/>.
    /// </summary>
    /// <remarks>
    /// A mutable brush is never frozen by the control: it draws with a frozen snapshot, and takes a new one
    /// when the brush changes, so the caller can go on changing it. Null here, with no fallback either, draws
    /// the series nothing.
    /// </remarks>
    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Gets or sets the stroke thickness, in DIP. The default is 2.</summary>
    /// <remarks>
    /// Styling only: it changes how wide the line is painted, never where it runs. Zero is accepted and draws
    /// nothing.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if negative or non-finite.</exception>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>
    /// Adds one point after the last point.
    /// </summary>
    /// <param name="point">The point; any values, NaN and infinity included.</param>
    /// <remarks>
    /// <para>
    /// The point becomes the last point, after every point the series already has, and the series redraws once.
    /// Nothing is sorted, merged, removed or checked: a duplicate is a new point, an x below the last x doubles
    /// the line back, a point outside the range is kept, and a point that is not finite is a gap, exactly as
    /// it would be in a list set on <see cref="Points"/>. The axis ranges are never changed.
    /// </para>
    /// <para>
    /// The list the caller set on <see cref="Points"/>, if any, is not changed: its points are copied, as they
    /// are at this call, into a list the series owns, and <see cref="Points"/> reads back that list from then
    /// on. See <see cref="Points"/>.
    /// </para>
    /// <para>
    /// To add many points, <see cref="AppendRange"/> adds them in one call and one redraw.
    /// </para>
    /// <para>
    /// Cost: an amortized constant. The point is written after the last one in an array the series owns, with room to
    /// spare, so a call copies no point whatever the count; the first append after <see cref="Points"/> is set copies
    /// that list once. No geometry is built here: the chart draws the points as they are when the frame is drawn, once
    /// however many appends came before it.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called from a thread other than the one the series belongs to, or when the series is frozen.
    /// </exception>
    public void Append(ChartPoint point)
    {
        WritePreamble();

        var buffer = OwnedBuffer(1);

        buffer.Add(point);
        SetCurrentValue(PointsProperty, buffer.Window());
    }

    /// <summary>
    /// Adds points after the last point, in the order given, in one update.
    /// </summary>
    /// <param name="points">The points to add. Read during the call only, never changed and never kept.</param>
    /// <remarks>
    /// <para>
    /// Exactly as if each point were given to <see cref="Append"/> in turn — the same points, in the same
    /// order, with the same gaps — except that the series redraws once, not once per point. An empty list
    /// changes nothing and redraws nothing.
    /// </para>
    /// <para>
    /// The points are copied into the series as they are during the call. The caller keeps the list: it may be
    /// changed or reused afterwards without affecting the series. A series' own <see cref="Points"/> may be
    /// passed, its own included.
    /// </para>
    /// <para>
    /// Cost, as for <see cref="Append"/>: room is made for the whole range at once and the range copied as one block, so
    /// a call costs time in proportion to the points added, not to the points already there, and asks for one redraw.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called from a thread other than the one the series belongs to, or when the series is frozen.
    /// </exception>
    public void AppendRange(IReadOnlyList<ChartPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        WritePreamble();

        var added = points.Count;

        if (added == 0)
        {
            return;
        }

        var buffer = OwnedBuffer(added);

        buffer.AddRange(points, added);
        SetCurrentValue(PointsProperty, buffer.Window());
    }

    /// <summary>
    /// Removes every point, leaving the series in its collection with no points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The series stays where it is in <see cref="FastiraChartControl.Series"/>, with its stroke, thickness and
    /// visibility; only its points go, and it redraws once. To take the series itself out of the chart, remove
    /// it from the collection instead.
    /// </para>
    /// <para>
    /// The list the caller set on <see cref="Points"/>, if any, is not changed; the series lets go of it.
    /// Clearing a series that was never given points, or is already cleared, changes nothing and redraws
    /// nothing.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called from a thread other than the one the series belongs to, or when the series is frozen.
    /// </exception>
    public void ClearPoints()
    {
        WritePreamble();

        // The array is let go of, not emptied: lists read before the clear keep their points.
        _buffer?.Clear();

        // The one empty list the property also defaults to, so clearing an already-cleared series is no change.
        SetCurrentValue(PointsProperty, Array.Empty<ChartPoint>());
    }

    /// <summary>Counts the changes to what this series' geometry is built from: its points.</summary>
    /// <remarks>
    /// <para>
    /// The control keeps each series' geometry between frames and builds it again only when this has
    /// moved on since — or when the range or the plot moved, which the control knows of itself. Every way the points
    /// change — <see cref="Points"/> set, <see cref="Append"/>, <see cref="AppendRange"/>, <see cref="ClearPoints"/> —
    /// sets the property, and the property's callback counts it. It is never the caller's list that decides: a list
    /// changed in place after it was set is no change, as <see cref="Points"/> documents.
    /// </para>
    /// <para>
    /// Internal: a cache key, not a public API. An integer that only grows, so it costs one increment per update.
    /// </para>
    /// </remarks>
    internal int GeometryGeneration { get; private set; }

    /// <summary>Counts the changes to how this series is stroked: its brush, a change inside it, its thickness.</summary>
    /// <remarks>
    /// A mutable <see cref="Stroke"/> changed in place reaches this series as a sub-property change of
    /// <see cref="StrokeProperty"/>, which runs the same callback, so the control knows the pen it snapshotted is out
    /// of date without comparing a colour. Internal, like <see cref="GeometryGeneration"/>.
    /// </remarks>
    internal int StyleGeneration { get; private set; }

    /// <summary>The buffer this series appends into, or null before its first append.</summary>
    /// <remarks>Read-only instrumentation for internal validation: its counters. Internal: not a public API.</remarks>
    internal SeriesPointBuffer? Buffer => _buffer;

    /// <summary>Creates a new, empty instance, as every <see cref="Freezable"/> must.</summary>
    /// <returns>A new <see cref="LineSeries"/>.</returns>
    protected override Freezable CreateInstanceCore() => new LineSeries();

    /// <summary>
    /// The buffer, holding exactly the current points, ready for <paramref name="adding"/> more after them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see cref="Points"/> is still the window the last append left — the steady state of a series that
    /// is appended to — the buffer already holds the points, and nothing is copied.
    /// </para>
    /// <para>
    /// Otherwise <see cref="Points"/> is a list the buffer does not own: the caller's, set to replace the
    /// points; one pushed by a binding; another series' window; or, for a clone, the window it was cloned
    /// with. Its content, as it is now, is copied in once, and the appends after this one write straight after
    /// it. The list itself is neither written nor kept.
    /// </para>
    /// </remarks>
    private SeriesPointBuffer OwnedBuffer(int adding)
    {
        var buffer = _buffer ??= new SeriesPointBuffer();
        var current = Points;

        if (!buffer.IsCurrent(current))
        {
            buffer.Load(current, adding);
        }

        return buffer;
    }

    /// <summary>Reads a null list back as an empty one, so a reader never has to test for it.</summary>
    private static object CoercePoints(DependencyObject d, object? value) => value ?? Array.Empty<ChartPoint>();

    /// <summary>Counts a change to the points; see <see cref="GeometryGeneration"/>.</summary>
    private static void OnGeometryInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LineSeries)d).GeometryGeneration++;

    /// <summary>Counts a change to the stroke, inside it, or to the thickness; see <see cref="StyleGeneration"/>.</summary>
    private static void OnStyleInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LineSeries)d).StyleGeneration++;

    /// <summary>Accepts a finite, non-negative thickness.</summary>
    private static bool IsValidThickness(object value) => value is double d && double.IsFinite(d) && d >= 0;
}
