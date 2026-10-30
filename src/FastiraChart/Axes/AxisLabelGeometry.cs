using System.Windows;
using System.Windows.Media;

namespace FastiraChart.Axes;

/// <summary>
/// The size of one label's unrotated text box.
/// </summary>
/// <param name="Width">Box width in DIP, as measured.</param>
/// <param name="Height">Box height in DIP, as measured.</param>
/// <remarks>
/// Deliberately just two numbers. Geometry takes measurements, not text and not a font, so it
/// can be checked against hand-computed values without rasterizing a glyph. Text measurement is
/// verified separately — mixing the two would mean every geometry expectation carried a font's
/// rounding with it, and a geometry regression would be indistinguishable from a font change.
/// </remarks>
internal readonly record struct LabelMetrics(double Width, double Height)
{
    /// <summary>True when both dimensions are finite and non-negative.</summary>
    public bool IsUsable => double.IsFinite(Width) && double.IsFinite(Height) && Width >= 0 && Height >= 0;
}

/// <summary>
/// Where one label is drawn, and the space it occupies.
/// </summary>
/// <param name="Transform">
/// Maps the unrotated text box — origin at its top-left corner — into the control's coordinate
/// space. Drawing is <c>DrawText</c> at the origin under this transform.
/// </param>
/// <param name="Bounds">The axis-aligned bounding rectangle after rotation, in control space.</param>
/// <param name="OutwardExtent">
/// How far the label reaches from the anchor point in the axis's outward direction. This is what
/// an axis band has to hold.
/// </param>
internal readonly record struct LabelPlacement(Matrix Transform, Rect Bounds, double OutwardExtent);

/// <summary>
/// Turns a label's measurements, anchor, rotation and anchor point into drawable geometry.
/// </summary>
/// <remarks>
/// <para>
/// Pure and deterministic: the result depends only on the arguments. Nothing here reads a font,
/// a DPI, a layout or a previous call, so the same label in the same place always lands in the
/// same place — a resize that does not move the tick cannot move the label.
/// </para>
/// <para>
/// <b>The pipeline.</b> In order, and the order matters:
/// </para>
/// <list type="number">
/// <item>Measure the text — done by the caller, arriving as <see cref="LabelMetrics"/>.</item>
/// <item>Pick the anchor on the unrotated box from <see cref="LabelAnchor"/>.</item>
/// <item>Take the anchor point from the tick: tick end, plus the label gap, outward.</item>
/// <item>Move the anchor to the origin.</item>
/// <item>Rotate about the origin.</item>
/// <item>Translate to the anchor point.</item>
/// <item>Clear outward, so the label's inward edge rests exactly on the anchor point.</item>
/// </list>
/// <para>
/// Steps 4 to 6 are what makes the anchor point the rotation origin, and they are why anchor and
/// rotation can be chosen independently: the anchor decides where on the text the pin goes, the
/// rotation decides how the text turns about that pin, and neither reaches into the other.
/// </para>
/// <para>
/// <b>Step 7 deserves its own justification</b>, because it is the one step the pipeline above
/// does not strictly require. Without it, a label on a top axis would hang downward into the plot
/// and a label on a left axis would straddle the plot edge: the anchor fixes a point on the text,
/// but a point does not say which side of it the rest of the text lies on. The clearance is a
/// pure translation along the outward normal, applied after the rotation, so it changes neither
/// the anchor's meaning nor the rotation's origin. What it buys is an exact guarantee: the gap
/// between the tick's end and the nearest part of the label is the requested gap, on every side
/// and at every angle.
/// </para>
/// <para>
/// <b>A consequence worth stating rather than discovering.</b> When the text's reading direction
/// is parallel to the outward direction — a vertical axis at 0° or 180°, a horizontal axis at 90°
/// or 270° — the anchor moves the label along the very axis the clearance then normalizes, so all
/// three anchors produce identical geometry. That is not a bug and not a collapse of the model:
/// the anchor positions text along its own reading direction, and on those six combinations that
/// direction has no component along the axis. The other six combinations distinguish all three
/// anchors. The validation suite asserts both halves of this, so the property is pinned rather
/// than merely true today.
/// </para>
/// <para>
/// Nothing here shortens, omits, abbreviates or re-angles a label. A label that does not fit is
/// still placed, and the caller is told how much room it wanted.
/// </para>
/// </remarks>
internal static class AxisLabelGeometry
{
    /// <summary>
    /// Places one label against an anchor point.
    /// </summary>
    /// <param name="metrics">The unrotated text box.</param>
    /// <param name="anchor">Which point along the text is pinned.</param>
    /// <param name="rotation">How far the text turns, clockwise on screen.</param>
    /// <param name="side">Which plot edge the axis occupies, which fixes outward.</param>
    /// <param name="anchorPoint">The pin, in control space: tick end plus the gap, outward.</param>
    /// <returns>
    /// The transform, the rotated bounds and the outward extent. An unusable measurement yields
    /// an empty placement at the anchor point rather than a non-finite one.
    /// </returns>
    public static LabelPlacement Place(
        LabelMetrics metrics,
        LabelAnchor anchor,
        LabelRotation rotation,
        AxisSide side,
        Point anchorPoint)
    {
        if (!metrics.IsUsable || !double.IsFinite(anchorPoint.X) || !double.IsFinite(anchorPoint.Y))
        {
            return new LabelPlacement(
                Matrix.Identity,
                new Rect(
                    double.IsFinite(anchorPoint.X) ? anchorPoint.X : 0,
                    double.IsFinite(anchorPoint.Y) ? anchorPoint.Y : 0,
                    0,
                    0),
                0);
        }

        var width = metrics.Width;
        var height = metrics.Height;

        // Step 2. Along the reading direction, from the anchor. Across it, the middle — a
        // rotation-independent choice, since the outward clearance in step 7 settles that
        // direction anyway and any other value would just shift what the clearance undoes.
        var anchorX = anchor switch
        {
            LabelAnchor.Start => 0d,
            LabelAnchor.End => width,
            _ => width / 2,
        };
        var anchorY = height / 2;

        // Steps 4 and 5, on the four corners. The rotation is applied as an exact coordinate
        // swap rather than through a sine, so a right angle stays a right angle to the last bit.
        var (x0, y0) = Rotate(0 - anchorX, 0 - anchorY, rotation);
        var (x1, y1) = Rotate(width - anchorX, 0 - anchorY, rotation);
        var (x2, y2) = Rotate(width - anchorX, height - anchorY, rotation);
        var (x3, y3) = Rotate(0 - anchorX, height - anchorY, rotation);

        var minX = Math.Min(Math.Min(x0, x1), Math.Min(x2, x3));
        var maxX = Math.Max(Math.Max(x0, x1), Math.Max(x2, x3));
        var minY = Math.Min(Math.Min(y0, y1), Math.Min(y2, y3));
        var maxY = Math.Max(Math.Max(y0, y1), Math.Max(y2, y3));

        // Step 7. Push along the outward normal until the inward edge sits on the anchor point.
        var (clearX, clearY) = side switch
        {
            AxisSide.Bottom => (0d, -minY),
            AxisSide.Top => (0d, -maxY),
            AxisSide.Left => (-maxX, 0d),
            _ => (-minX, 0d),
        };

        // Step 6 and the clearance, as one translation.
        var offsetX = anchorPoint.X + clearX;
        var offsetY = anchorPoint.Y + clearY;

        // Steps 4 to 6 as one matrix, built from the same exact per-case mapping the corners went
        // through. It used to be composed with Matrix.Rotate, which goes through Math.Cos and
        // Math.Sin and so drew a 90° label under M11 = 6.1e-17 while the bounds above were exact:
        // the drawn geometry and the published geometry disagreed in the last bits, which shows as soon as
        // the drawn transform is read back and compared with the published one.
        var transform = RotationMatrix(rotation);
        var (pinX, pinY) = Rotate(0 - anchorX, 0 - anchorY, rotation);

        transform.OffsetX = offsetX + pinX;
        transform.OffsetY = offsetY + pinY;

        var bounds = new Rect(offsetX + minX, offsetY + minY, maxX - minX, maxY - minY);

        var outwardExtent = side.IsVerticalOutward() ? maxY - minY : maxX - minX;

        return new LabelPlacement(transform, bounds, outwardExtent);
    }

    /// <summary>
    /// The axis thickness a side needs to hold its ticks and labels.
    /// </summary>
    /// <param name="tickLength">The requested tick length, not the granted one.</param>
    /// <param name="labelGap">The gap between a tick's end and its label.</param>
    /// <param name="maximumOutwardExtent">The largest outward extent among that side's labels.</param>
    /// <returns>A finite, non-negative thickness.</returns>
    /// <remarks>
    /// The requested tick length is used rather than the effective one on purpose. This figure
    /// says how much room the axis <em>wants</em>; feeding back the length that a previous,
    /// smaller grant allowed would let the axis argue itself steadily thinner.
    /// </remarks>
    public static double RequiredAxisThickness(double tickLength, double labelGap, double maximumOutwardExtent)
    {
        var total = NonNegativeFinite(tickLength)
            + NonNegativeFinite(labelGap)
            + NonNegativeFinite(maximumOutwardExtent);

        return double.IsFinite(total) ? total : 0;
    }

    /// <summary>The angle in degrees, clockwise on screen.</summary>
    public static double DegreesOf(LabelRotation rotation) => rotation switch
    {
        LabelRotation.Degree90 => 90,
        LabelRotation.Degree180 => 180,
        LabelRotation.Degree270 => 270,
        _ => 0,
    };

    /// <summary>
    /// Rotates a point about the origin, clockwise on screen.
    /// </summary>
    /// <remarks>
    /// Written out per case instead of calling into trigonometry. <c>Math.Cos(Math.PI / 2)</c> is
    /// 6.1e-17, not 0, and that residue would smear a 90° label by a fraction of a DIP and make
    /// an exact expected value impossible to write down.
    /// </remarks>
    private static (double X, double Y) Rotate(double x, double y, LabelRotation rotation) => rotation switch
    {
        LabelRotation.Degree90 => (-y, x),
        LabelRotation.Degree180 => (-x, -y),
        LabelRotation.Degree270 => (y, -x),
        _ => (x, y),
    };

    /// <summary>
    /// The linear part of <see cref="Rotate"/>, as a matrix with no translation.
    /// </summary>
    /// <remarks>
    /// WPF maps a point as <c>x' = x·M11 + y·M21</c> and <c>y' = x·M12 + y·M22</c>, so each case is
    /// the coefficients of the mapping above read off directly. Every entry is exactly −1, 0 or 1.
    /// </remarks>
    private static Matrix RotationMatrix(LabelRotation rotation) => rotation switch
    {
        LabelRotation.Degree90 => new Matrix(0, 1, -1, 0, 0, 0),
        LabelRotation.Degree180 => new Matrix(-1, 0, 0, -1, 0, 0),
        LabelRotation.Degree270 => new Matrix(0, -1, 1, 0, 0, 0),
        _ => Matrix.Identity,
    };

    private static double NonNegativeFinite(double value)
        => double.IsFinite(value) && value > 0 ? value : 0;
}
