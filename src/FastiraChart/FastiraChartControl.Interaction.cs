using System.Windows;
using System.Windows.Input;
using FastiraChart.Interaction;

namespace FastiraChart;

// <summary>
// The pointer over the chart: where it is, and what it stands for. State only — nothing is drawn from it,
// and nothing of the layout or the layers is touched by it.
// </summary>
// <remarks>
// <para>
// <b>Ordinary routed input.</b> The pointer is heard through the control's own <see cref="OnMouseEnter"/>,
// <see cref="OnMouseMove"/> and <see cref="OnMouseLeave"/> overrides: no handler is subscribed, so there is nothing to
// remove and nothing that outlives the control; a pointer move captures nothing and is never marked handled. A move
// also carries a drag in progress (<c>FastiraChartControl.Gestures.cs</c>); the pointer state is unchanged by it.
// </para>
// <para>
// <b>Only the coordinate is kept.</b> What the pointer stands for is resolved when it is read, from the plot every layer
// was last placed against, the current ranges and the current lens state — so a resize, a range change or a lens move can
// never leave a stale reading behind, and a move costs two stores. A read allocates nothing.
// </para>
// <para>
// <b>Drawn on demand.</b> With the crosshair or the tooltip on, a move also marks the interaction overlay — that
// layer alone — which resolves the stored point when it is next rendered (<c>FastiraChartControl.Overlay.cs</c>). With
// both off a move still marks nothing.
// </para>
// <para>
// Internal: the pointer state, the inverse and the hit regions stay internal; the public interaction API is
// the five switches.
// </para>
// </remarks>
public partial class FastiraChartControl
{
    private Point _pointerScreen;
    private bool _hasPointer;
    private int _pointerUpdates;

    /// <summary>The pointer resolved against the chart as it stands now, or null while no pointer is over the control.</summary>
    internal ChartPointerPosition? PointerState => _hasPointer ? ResolvePointer(_pointerScreen) : null;

    /// <summary>How many pointer updates the control has taken: enters, moves and leaves.</summary>
    /// <remarks>Read-only instrumentation for internal validation. Internal: it is not a public API.</remarks>
    internal int PointerUpdates => _pointerUpdates;

    /// <summary>Resolves a coordinate in the control's space against the chart as it stands now.</summary>
    /// <remarks>
    /// Against <c>_tickPlotBounds</c> — the rectangle every layer is placed against, in this control's space, which is the
    /// space <see cref="MouseEventArgs.GetPosition"/> reports in when handed this control — and the lens placed as the lens
    /// layer places it. Reads state only; builds, invalidates and caches nothing.
    /// </remarks>
    internal ChartPointerPosition ResolvePointer(Point screen)
        => PointerMapping.Resolve(screen, _tickPlotBounds, MinimumX, MaximumX, MinimumY, MaximumY, LensState);

    /// <summary>Records the pointer at a coordinate in the control's space.</summary>
    /// <remarks>What the mouse overrides call; internal validation calls it to drive many moves at chosen coordinates.</remarks>
    internal void UpdatePointer(Point screen)
    {
        _pointerScreen = screen;
        _hasPointer = true;
        _pointerUpdates++;
        RefreshInteraction();
    }

    /// <summary>Records that no pointer is over the control.</summary>
    internal void ClearPointer()
    {
        _hasPointer = false;
        _pointerUpdates++;
        RefreshInteraction();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdatePointer(e.GetPosition(this));
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var at = e.GetPosition(this);

        UpdatePointer(at);
        ContinueDrag(e, at);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ClearPointer();

        // A captured drag follows the pointer off the control; one that lost its capture ends here.
        if (_drag != InteractionDrag.None && !IsMouseCaptured)
        {
            EndDrag();
        }
    }
}
