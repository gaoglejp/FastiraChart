namespace FastiraChart.Axes;

/// <summary>
/// Which screen edge of the plot an axis that runs horizontally — the X axis — is drawn on.
/// </summary>
/// <remarks>
/// Used by <see cref="FastiraChartControl.XAxisPosition"/>. A horizontal axis can take only the bottom or the top edge.
/// <see cref="Bottom"/> is first so that it is also <c>default</c>, the same value as the control's default. The control
/// property rejects any value that is not one of the two members.
/// </remarks>
public enum HorizontalAxisPosition
{
    /// <summary>Along the bottom edge; ticks extend downward into the bottom axis band. The default.</summary>
    Bottom,

    /// <summary>Along the top edge; ticks extend upward into the top axis band.</summary>
    Top,
}

/// <summary>
/// Which screen edge of the plot an axis that runs vertically — the Y axis — is drawn on.
/// </summary>
/// <remarks>
/// Used by <see cref="FastiraChartControl.YAxisPosition"/>. A vertical axis can take only the left or the right edge.
/// <see cref="Left"/> is first so that it is also <c>default</c>, the same value as the control's default. The control
/// property rejects any value that is not one of the two members.
/// </remarks>
public enum VerticalAxisPosition
{
    /// <summary>Along the left edge; ticks extend leftward into the left axis band. The default.</summary>
    Left,

    /// <summary>Along the right edge; ticks extend rightward into the right axis band.</summary>
    Right,
}
