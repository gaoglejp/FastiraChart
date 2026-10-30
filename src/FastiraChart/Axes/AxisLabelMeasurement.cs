using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FastiraChart.Axes;

/// <summary>
/// Measures label text with WPF's own text stack.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FormattedText"/> rather than a glyph-level API. It is the measurement WPF itself
/// uses when it lays text out, so a label measured here occupies exactly what it occupies when
/// drawn — which a hand-rolled glyph sum would only approximate, and would have to re-derive
/// shaping, kerning and fallback to do even that. The same object is then drawn, so measurement
/// and rendering cannot disagree: there is only one of them.
/// </para>
/// <para>
/// <b>DPI.</b> <c>pixelsPerDip</c> is a required constructor argument on
/// <see cref="FormattedText"/> and is passed through from the live visual, never assumed to be
/// 1.0. Hard-coding 96 DPI would make every measurement wrong by the scaling factor on the
/// 150% displays this project develops on, and wrong in the direction that looks plausible —
/// labels slightly too small for their reserved band rather than obviously broken.
/// </para>
/// <para>
/// No external package. This is WPF's built-in text stack and nothing else.
/// </para>
/// </remarks>
internal static class AxisLabelMeasurement
{
    /// <summary>
    /// The smallest font size that will be measured.
    /// </summary>
    /// <remarks>
    /// <see cref="FormattedText"/> throws on a non-positive size, and a bound control can carry
    /// one transiently. Clamping keeps a measurement pass from throwing in the middle of an
    /// arrange, where the exception would surface far from its cause.
    /// </remarks>
    private const double MinimumFontSize = 0.01;

    /// <summary>
    /// Builds the measured text for one label.
    /// </summary>
    /// <param name="text">The label text.</param>
    /// <param name="typeface">The typeface, built from the control's font properties.</param>
    /// <param name="fontSize">Font size in DIP.</param>
    /// <param name="brush">The brush the text will be drawn with.</param>
    /// <param name="pixelsPerDip">The live scaling factor, from the visual being drawn into.</param>
    /// <param name="culture">The culture the text is formatted and shaped for.</param>
    /// <returns>Measured text, ready to draw.</returns>
    public static FormattedText Create(
        string text,
        Typeface typeface,
        double fontSize,
        Brush brush,
        double pixelsPerDip,
        CultureInfo culture)
    {
        var size = double.IsFinite(fontSize) && fontSize > MinimumFontSize ? fontSize : MinimumFontSize;
        var scale = double.IsFinite(pixelsPerDip) && pixelsPerDip > 0 ? pixelsPerDip : 1.0;

        return new FormattedText(
            text,
            culture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            brush,
            scale);
    }

    /// <summary>
    /// Reads the unrotated box out of measured text.
    /// </summary>
    /// <param name="text">Measured text.</param>
    /// <returns>Its width and height, clamped to finite and non-negative.</returns>
    /// <remarks>
    /// Clamped rather than trusted. An empty string measures zero wide, which is legitimate, and
    /// geometry handles it; a non-finite measurement is not legitimate and must not be allowed to
    /// propagate into a <see cref="Rect"/>, which would then report non-finite bounds to every
    /// consumer downstream.
    /// </remarks>
    public static LabelMetrics MetricsOf(FormattedText text) => new(
        double.IsFinite(text.Width) && text.Width > 0 ? text.Width : 0,
        double.IsFinite(text.Height) && text.Height > 0 ? text.Height : 0);
}
