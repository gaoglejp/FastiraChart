using System.Windows;
using System.Windows.Media;
using FastiraChart.Rendering;

namespace FastiraChart.Series;

/// <summary>
/// One series ready to draw: where it came from, its geometry, and how it is stroked.
/// </summary>
/// <param name="Index">Its position in <see cref="FastiraChartControl.Series"/>.</param>
/// <param name="Geometry">Its geometry and what went into it.</param>
/// <param name="Pen">The pen to stroke it with, or null when its style draws nothing.</param>
/// <remarks>
/// The index is kept so internal validation can tie every drawn geometry back to the series that asked for
/// it; two identical series are two entries, and the index is what tells them apart.
/// </remarks>
internal readonly record struct PlacedSeries(int Index, SeriesGeometry Geometry, Pen? Pen);

/// <summary>
/// Everything one frame of series draws: every series ready to stroke, and the clip.
/// </summary>
/// <param name="Series">The visible series, in collection order.</param>
/// <param name="Plot">The plot rectangle.</param>
/// <param name="Clip">Whether to clip the stroke to it.</param>
internal readonly record struct SeriesFrame(IReadOnlyList<PlacedSeries> Series, Rect Plot, bool Clip);

/// <summary>
/// Draws every series for the whole control.
/// </summary>
/// <remarks>
/// <para>
/// The series drawing stage. It strokes the geometry and pens the control's builder gives it; it
/// computes no coordinate of its own.
/// </para>
/// <para>
/// <b>One element, whatever the series or point count</b>, like the grid, reference and tick layers. Each
/// series is one <see cref="StreamGeometry"/> stroked into this element's <see cref="DrawingContext"/>, so ten
/// thousand points add no element and no visual, and neither does a tenth series.
/// </para>
/// <para>
/// <b>Z-order inside the layer is collection order</b>: the first series is drawn first, and a series added
/// later is drawn over it. The layer as a whole sits above the origin and reference lines and below the axes;
/// that order is the template's.
/// </para>
/// <para>
/// <b>Clipping follows <see cref="FastiraChartControl.ClipToPlotArea"/></b>. When it is true the stroke is
/// clipped to the plot rectangle, so a point off the plot, and the part of a segment that leaves it, are not
/// seen. When it is false nothing is clipped here: the layer's host spans the whole control and is not clipped
/// either, so the lines are drawn wherever they fall — over the axis bands, beneath the ticks and labels, and
/// past the control's edge as far as its host allows. That is what the content host has always done with the
/// same setting.
/// </para>
/// <para>
/// <b>Built when drawn, not when changed</b>. A change — new points, a new range, a resize, a
/// style — only marks the frame stale and asks for a redraw (<see cref="DeferredLayer{TFrame}.Invalidate"/>).
/// The geometry is built from the series as they are when WPF next renders this element, by the builder the
/// control gave it, and then kept until the next change. A thousand appends before a frame are a thousand
/// invalidations and one build; WPF coalesces the redraw requests into one render. The mechanism
/// is <see cref="DeferredLayer{TFrame}"/>, shared with every other layer.
/// </para>
/// <para>
/// <b>Kept from one build to the next</b>. A build no longer builds every visible series: the control
/// keeps each series' geometry until its points, the range or the plot change, so a build after a stroke change
/// strokes the very geometry the last frame drew, and a build after an append to one series builds that one.
/// </para>
/// </remarks>
internal sealed class SeriesLayer : DeferredLayer<SeriesFrame>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesLayer"/> class.
    /// </summary>
    /// <param name="build">Builds the frame from the series as they are when it is called.</param>
    public SeriesLayer(Func<SeriesFrame> build)
        : base(build, new SeriesFrame([], default, true))
    {
    }

    /// <summary>The series as they are drawn now, in drawing order.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, which compares what was generated here with the
    /// collection and, separately, with what was drawn, read back from the render data. Reading it brings the
    /// frame up to date first, building it if it is stale, exactly as the next render would; so it is always
    /// what the current state draws, and it is the very same list for as long as nothing changes. Internal: it
    /// is not a public geometry API.
    /// </remarks>
    internal IReadOnlyList<PlacedSeries> Placed => Current.Series;

    /// <summary>The plot rectangle the stroke is clipped to when clipping is on.</summary>
    internal Rect PlotClip => Current.Plot;

    /// <summary>Whether the stroke is clipped to <see cref="PlotClip"/>.</summary>
    internal bool Clips => Current.Clip;

    /// <inheritdoc />
    protected override void Draw(DrawingContext drawingContext, SeriesFrame frame)
    {
        if (frame.Series.Count == 0)
        {
            return;
        }

        if (frame.Clip)
        {
            drawingContext.PushClip(ClipFor(frame.Plot));
        }

        foreach (var series in frame.Series)
        {
            if (series.Pen is { } pen && series.Geometry.Geometry is { } geometry)
            {
                drawingContext.DrawGeometry(null, pen, geometry);
            }
        }

        if (frame.Clip)
        {
            drawingContext.Pop();
        }
    }
}
