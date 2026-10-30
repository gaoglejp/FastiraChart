using System.Windows.Media;

namespace FastiraChart.Rendering;

/// <summary>
/// One part of a layer's frame — one axis's labels, say — drawn straight into the render data while it changes, and
/// replayed from a recording once it has been drawn twice unchanged.
/// </summary>
/// <typeparam name="TPart">The part: a list the control caches and never changes after it is made.</typeparam>
/// <remarks>
/// <para>
/// WPF's retained mode keeps what a layer drew, but a layer that is rendered again draws <em>everything</em>
/// again: a horizontal range that moves the bottom labels also re-draws the left labels, and a dense axis's
/// <see cref="DrawingContext.DrawText"/> lays out every string again on every draw, whether it moved or not. Measured on a
/// 1001-labels-per-axis chart: 31 ms a frame with a frozen brush, 64 ms with a mutable one,
/// almost all of it in <c>DrawText</c>.
/// </para>
/// <para>
/// So a part the control hands the layer unchanged — the very same list, drawn in the very same style — is recorded
/// once into a <see cref="DrawingGroup"/> and from then on referenced: a render of the layer then costs one
/// <see cref="DrawingContext.DrawDrawing"/> for that part. A part that changes is drawn directly, exactly as before,
/// and is recorded only at the second render that finds it unchanged, because recording costs more than one direct draw
/// (about 1.5×): a part that changes every frame — the horizontal axis's labels while the chart scrolls — is never
/// recorded at all.
/// </para>
/// <para>
/// <b>Not a raster cache.</b> The recording is the same vector drawing commands — the same glyph runs, the same
/// transforms, the same brush — that drawing directly emits, kept as a retained WPF <see cref="Drawing"/>. Nothing is
/// rasterized, no bitmap is kept, and WPF renders it exactly as it renders the direct commands, ClearType included.
/// </para>
/// <para>
/// <b>Never in a held render.</b> While the control arranges its template the layers are held
/// (<see cref="DeferredLayer{TFrame}.Hold"/>) and a render draws the frame the layer already had, to be replaced in the
/// same layout pass. Such a render replays a recording it has but never makes one: a resize renders every layer held
/// with its old frame on every step, and recording that frame would be paid for and thrown away at once.
/// </para>
/// <para>
/// <b>Ownership.</b> The recording is FastiraChart's. It is frozen only when its style is a frozen brush or pen: a
/// caller's mutable brush is never frozen — the recording references it live, as the direct drawing does, so a change
/// inside it shows without anything being recorded again. Only the latest recording of the part is kept; a part that
/// changes drops it.
/// </para>
/// </remarks>
internal sealed class RecordedPart<TPart>
    where TPart : class
{
    private TPart? _source;
    private object? _style;
    private DrawingGroup? _recording;

    /// <summary>How many times the part was recorded.</summary>
    /// <remarks>Read-only instrumentation for internal validation.</remarks>
    internal int Recordings { get; private set; }

    /// <summary>How many draws replayed the recording instead of drawing the part again.</summary>
    /// <remarks>Read-only instrumentation for internal validation.</remarks>
    internal int Replays { get; private set; }

    /// <summary>Whether a recording is held: zero or one, never more.</summary>
    internal bool IsRecorded => _recording is not null;

    /// <summary>The recording held, or null.</summary>
    /// <remarks>
    /// Read-only instrumentation for internal validation, which holds it only weakly to prove a dropped recording —
    /// one drawn in a caller's mutable brush among them — is collectable.
    /// </remarks>
    internal Drawing? Recording => _recording;

    /// <summary>Whether the last draw went through the recording (made then, or replayed) rather than drawing directly.</summary>
    /// <remarks>Read-only instrumentation for internal validation.</remarks>
    internal bool Replayed { get; private set; }

    /// <summary>Draws the part, directly or from its recording.</summary>
    /// <param name="drawingContext">The layer's drawing context.</param>
    /// <param name="part">The part to draw.</param>
    /// <param name="style">
    /// What the part is drawn with — the brush the text carries, the pen's brush and thickness — compared by
    /// <see cref="object.Equals(object?)"/>, which for a brush is its identity. A change inside a mutable brush keeps the
    /// identity and is drawn live by the recording, as it is by the direct drawing.
    /// </param>
    /// <param name="frozen">Whether the style is frozen, so the recording may be frozen with it.</param>
    /// <param name="mayRecord">False in a held render: an existing recording is replayed, none is made.</param>
    /// <param name="draw">Draws the part into a context.</param>
    public void Draw(DrawingContext drawingContext, TPart part, object? style, bool frozen, bool mayRecord, Action<DrawingContext, TPart> draw)
    {
        if (ReferenceEquals(part, _source) && Equals(style, _style) && (_recording is not null || mayRecord))
        {
            if (_recording is null)
            {
                var recording = new DrawingGroup();

                using (var context = recording.Open())
                {
                    draw(context, part);
                }

                // A frozen style only: freezing the recording would freeze a caller's mutable brush in it.
                if (frozen && recording.CanFreeze)
                {
                    recording.Freeze();
                }

                _recording = recording;
                Recordings++;
            }
            else
            {
                Replays++;
            }

            Replayed = true;
            drawingContext.DrawDrawing(_recording);

            return;
        }

        if (!ReferenceEquals(part, _source) || !Equals(style, _style))
        {
            _source = part;
            _style = style;
            _recording = null;
        }

        Replayed = false;
        draw(drawingContext, part);
    }
}
