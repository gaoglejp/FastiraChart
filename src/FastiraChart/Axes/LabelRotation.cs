namespace FastiraChart.Axes;

/// <summary>
/// The angle a tick label is drawn at.
/// </summary>
/// <remarks>
/// <para>
/// An enum rather than a number, because only four angles are supported. The four right angles
/// are what axis labels use, and each one maps to an exact coordinate swap rather than a
/// trigonometric result, so no placement depends on the rounding of a sine.
/// </para>
/// <para>
/// <b>Direction.</b> Positive degrees turn <b>clockwise on screen</b>. WPF's coordinate space
/// has y growing downward, so a clockwise visual turn is the positive mathematical direction in
/// that space, and these values match <see cref="System.Windows.Media.RotateTransform"/> and
/// <see cref="System.Windows.Media.Matrix.Rotate"/> exactly. Concretely, at
/// <see cref="Degree90"/> text reads top-to-bottom.
/// </para>
/// <para>
/// The rotation is chosen by the caller and never by the control. Nothing — a narrow window, a
/// crowded axis, a long label — changes it automatically.
/// </para>
/// <para>
/// The control properties that take it (<see cref="FastiraChartControl.XAxisLabelRotation"/>,
/// <see cref="FastiraChartControl.YAxisLabelRotation"/>) reject a value that is not one of the four members.
/// </para>
/// </remarks>
public enum LabelRotation
{
    /// <summary>Horizontal text, reading left to right. The default.</summary>
    Degree0,

    /// <summary>Quarter turn clockwise; text reads top to bottom.</summary>
    Degree90,

    /// <summary>Half turn; text reads right to left, upside down.</summary>
    Degree180,

    /// <summary>Three-quarter turn clockwise; text reads bottom to top.</summary>
    Degree270,
}
