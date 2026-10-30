namespace FastiraChart.Axes;

/// <summary>
/// Which edge of the plot an axis occupies, as one type covering both axes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HorizontalAxisPosition"/> and <see cref="VerticalAxisPosition"/> remain the public
/// surface, because they make an impossible setting — a horizontal axis on the left — impossible
/// to express. Label geometry does not care which axis a side belongs to, only which way is
/// outward, so it takes this instead of branching on two enums at every step.
/// </para>
/// <para>
/// Internal: it is a convenience for the geometry, not a widening of the public model.
/// </para>
/// </remarks>
internal enum AxisSide
{
    /// <summary>Below the plot; outward is down, +y.</summary>
    Bottom,

    /// <summary>Above the plot; outward is up, −y.</summary>
    Top,

    /// <summary>Left of the plot; outward is left, −x.</summary>
    Left,

    /// <summary>Right of the plot; outward is right, +x.</summary>
    Right,
}

/// <summary>Conversions from the public axis positions to <see cref="AxisSide"/>.</summary>
internal static class AxisSideExtensions
{
    /// <summary>Returns the side a horizontal axis occupies.</summary>
    public static AxisSide ToSide(this HorizontalAxisPosition position)
        => position == HorizontalAxisPosition.Top ? AxisSide.Top : AxisSide.Bottom;

    /// <summary>Returns the side a vertical axis occupies.</summary>
    public static AxisSide ToSide(this VerticalAxisPosition position)
        => position == VerticalAxisPosition.Right ? AxisSide.Right : AxisSide.Left;

    /// <summary>True when the side's outward direction runs along y.</summary>
    public static bool IsVerticalOutward(this AxisSide side)
        => side is AxisSide.Bottom or AxisSide.Top;
}
