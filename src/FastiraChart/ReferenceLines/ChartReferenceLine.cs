using System.Windows;
using System.Windows.Media;

namespace FastiraChart.ReferenceLines;

/// <summary>
/// A line drawn across the plot at a value chosen by the caller.
/// </summary>
/// <remarks>
/// <para>
/// Added to <see cref="FastiraChartControl.ReferenceLines"/>. The line marks <see cref="Value"/> in the
/// value dimension named by <see cref="Axis"/>: an X value is drawn as a vertical screen line, a Y value
/// as a horizontal one — see <see cref="ReferenceLineAxis"/>.
/// </para>
/// <para>
/// <b>Plot content, not an axis and not a grid.</b> It is placed by the same value-to-coordinate mapping
/// the ticks and the grid use, so a reference line at a tick's value lands exactly on that tick's grid
/// line. It is drawn only inside the plot, and nothing about it — its value, its visibility, its style or
/// how many there are — takes any part in the layout.
/// </para>
/// <para>
/// <b>A value outside the axis range is not drawn</b>, and is not moved to the nearest edge either: an
/// edge line would claim a value the plot does not show. The line stays in the collection and appears
/// again as soon as the range includes its value. A value exactly equal to the minimum or the maximum is
/// inside, and its line is drawn on the plot edge.
/// </para>
/// <para>
/// <b>A <see cref="Freezable"/></b>, which is what makes it WPF-native without any event wiring of its
/// own. A change to any of its properties — or to a mutable <see cref="Stroke"/> brush it holds — notifies the
/// collection, and the collection notifies the control, which redraws. No subscription is made that would
/// have to be removed, so removing a line from the collection is all it takes to detach it. Properties can
/// be bound, and a line can be frozen by the caller when it will not change.
/// </para>
/// <para>
/// <b>Caller-owned.</b> FastiraChart never freezes or modifies a line, or the brush it holds.
/// </para>
/// </remarks>
public sealed class ChartReferenceLine : Freezable
{
    /// <summary>Identifies the <see cref="Axis"/> dependency property.</summary>
    public static readonly DependencyProperty AxisProperty =
        DependencyProperty.Register(
            nameof(Axis),
            typeof(ReferenceLineAxis),
            typeof(ChartReferenceLine),
            new PropertyMetadata(ReferenceLineAxis.X, OnGeometryInputChanged),
            IsDefinedAxis);

    /// <summary>Identifies the <see cref="Value"/> dependency property.</summary>
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(ChartReferenceLine),
            new PropertyMetadata(0d, OnGeometryInputChanged),
            IsFiniteDouble);

    /// <summary>Identifies the <see cref="IsVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsVisibleProperty =
        DependencyProperty.Register(
            nameof(IsVisible),
            typeof(bool),
            typeof(ChartReferenceLine),
            new PropertyMetadata(true, OnGeometryInputChanged));

    /// <summary>Identifies the <see cref="Stroke"/> dependency property.</summary>
    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(
            nameof(Stroke),
            typeof(Brush),
            typeof(ChartReferenceLine),
            new PropertyMetadata(null, OnStyleInputChanged));

    /// <summary>Identifies the <see cref="StrokeThickness"/> dependency property.</summary>
    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(ChartReferenceLine),
            new PropertyMetadata(DefaultStrokeThickness, OnStyleInputChanged),
            IsValidThickness);

    /// <summary>Initial stroke thickness of a reference line, in DIP.</summary>
    /// <remarks>
    /// One DIP, like a grid line: the line is told apart from the grid by its colour, not by its weight,
    /// and the origin's heavier default stays the heaviest line in the plot. Not rounded to a device
    /// pixel.
    /// </remarks>
    internal const double DefaultStrokeThickness = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChartReferenceLine"/> class: an X-value line at 0.
    /// </summary>
    public ChartReferenceLine()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChartReferenceLine"/> class at a value on an axis.
    /// </summary>
    /// <param name="axis">The value dimension: <see cref="ReferenceLineAxis.X"/> or <see cref="ReferenceLineAxis.Y"/>.</param>
    /// <param name="value">The value to mark; must be finite.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="axis"/> is not a defined member, or <paramref name="value"/> is NaN or infinite.
    /// </exception>
    public ChartReferenceLine(ReferenceLineAxis axis, double value)
    {
        Axis = axis;
        Value = value;
    }

    /// <summary>
    /// Gets or sets the value dimension the line marks. The default is <see cref="ReferenceLineAxis.X"/>: an X value,
    /// drawn as a vertical screen line.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the value is not a defined member.</exception>
    public ReferenceLineAxis Axis
    {
        get => (ReferenceLineAxis)GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    /// <summary>Gets or sets the value the line marks, in the data units of its axis. The default is 0.</summary>
    /// <remarks>
    /// Any finite value is accepted, inside the axis range or not; a value outside the range is kept and
    /// simply not drawn.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if NaN or infinite.</exception>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Gets or sets whether the line is drawn. The default is true.</summary>
    /// <remarks>Drawing only: a hidden line keeps its place in the collection and changes no layout.</remarks>
    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush the line is stroked with. The default is null, which uses the control's
    /// <see cref="FastiraChartControl.ReferenceLineBrush"/>.
    /// </summary>
    /// <remarks>
    /// The fallback lets every line follow the control's style without each one naming a colour; setting
    /// a brush here overrides it for this line alone. The brush is the caller's and is never frozen or modified: the
    /// control draws with a frozen snapshot and takes a new one when a mutable brush changes.
    /// </remarks>
    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Gets or sets the stroke thickness, in DIP. The default is 1.</summary>
    /// <remarks>
    /// Styling only: it changes how wide the line is painted, never where it is. Zero is accepted and
    /// draws nothing. The stroke is clipped to the plot, so a thick line on an edge is cut at the edge.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown by the property system if negative or non-finite.</exception>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Counts the changes to where this line is drawn: its value, its axis, its visibility.</summary>
    /// <remarks>
    /// The control keeps each line's placed geometry between frames and places it again only when this
    /// has moved on since — or when the range or the plot moved, which the control knows of itself. Internal: a cache
    /// key, not a public API.
    /// </remarks>
    internal int GeometryGeneration { get; private set; }

    /// <summary>Counts the changes to how this line is stroked: its brush, a change inside it, its thickness.</summary>
    /// <remarks>
    /// A mutable <see cref="Stroke"/> changed in place reaches this line as a sub-property change of
    /// <see cref="StrokeProperty"/>, which runs the same callback. Internal, like <see cref="GeometryGeneration"/>.
    /// </remarks>
    internal int StyleGeneration { get; private set; }

    /// <summary>Creates a new, empty instance, as every <see cref="Freezable"/> must.</summary>
    /// <returns>A new <see cref="ChartReferenceLine"/>.</returns>
    protected override Freezable CreateInstanceCore() => new ChartReferenceLine();

    /// <summary>Counts a change to the value, the axis or the visibility; see <see cref="GeometryGeneration"/>.</summary>
    private static void OnGeometryInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ChartReferenceLine)d).GeometryGeneration++;

    /// <summary>Counts a change to the brush, inside it, or to the thickness; see <see cref="StyleGeneration"/>.</summary>
    private static void OnStyleInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ChartReferenceLine)d).StyleGeneration++;

    /// <summary>Accepts any finite double; NaN and infinity have no position on any scale.</summary>
    /// <remarks>
    /// Refused at the property, as every other numeric input to the control is, rather than stored and
    /// skipped when drawing: a line that silently held NaN would be a line nobody could see or explain.
    /// </remarks>
    private static bool IsFiniteDouble(object value) => value is double d && double.IsFinite(d);

    /// <summary>Accepts a finite, non-negative thickness.</summary>
    private static bool IsValidThickness(object value) => value is double d && double.IsFinite(d) && d >= 0;

    /// <summary>Accepts only the defined members, so a cast integer cannot pick a third direction.</summary>
    private static bool IsDefinedAxis(object value)
        => value is ReferenceLineAxis axis && axis is ReferenceLineAxis.X or ReferenceLineAxis.Y;
}
