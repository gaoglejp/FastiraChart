namespace FastiraChart.Axes;

/// <summary>
/// Which point along a tick label's text is pinned to the label anchor point.
/// </summary>
/// <remarks>
/// <para>
/// The anchor is a position along the <b>text's own reading direction</b>, measured on the
/// unrotated text box. It is not a screen direction, and rotation does not change what it
/// means: <see cref="Start"/> is the first character's edge whether the text reads left to
/// right, top to bottom or upside down.
/// </para>
/// <para>
/// Keeping the anchor in text space is what lets anchor and rotation be chosen independently.
/// An anchor defined as "left" or "top" would silently change meaning at every quarter turn,
/// and the twelve anchor-rotation combinations would stop being twelve independent cases.
/// </para>
/// </remarks>
public enum LabelAnchor
{
    /// <summary>The leading edge of the text, at x = 0 on the unrotated box.</summary>
    Start,

    /// <summary>The middle of the text, at x = width / 2 on the unrotated box. The default.</summary>
    Center,

    /// <summary>The trailing edge of the text, at x = width on the unrotated box.</summary>
    End,
}
