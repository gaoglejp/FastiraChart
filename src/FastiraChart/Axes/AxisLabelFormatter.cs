using System.Globalization;

namespace FastiraChart.Axes;

/// <summary>
/// Turns a major tick's value into the text drawn beside it.
/// </summary>
/// <remarks>
/// <para>
/// Linear numeric ticks only, and deliberately small. A formatting framework built now — custom
/// patterns, category text, date-time text, unit suffixes — would be designed against one caller
/// and guessed for the rest. What is provided is a seam, not a framework: the
/// format provider is already a parameter, and the one method here is the only place a tick value
/// becomes a string, so a formatter abstraction has exactly one site to replace when there is a
/// second kind of axis to justify it.
/// </para>
/// <para>
/// <b>Culture.</b> The provider is always passed in; nothing here reads an ambient culture. The
/// control supplies <see cref="CultureInfo.CurrentCulture"/>, so a decimal comma appears where the
/// user's settings call for one, and the validation suite supplies
/// <see cref="CultureInfo.InvariantCulture"/>, so its expected strings are fixed. Both are stated
/// rather than defaulted, because a formatter that silently reads the ambient culture produces a
/// suite that passes on the machine that wrote it.
/// </para>
/// </remarks>
internal static class AxisLabelFormatter
{
    /// <summary>
    /// The pattern tick values are rendered with.
    /// </summary>
    /// <remarks>
    /// Plain decimal, up to ten fractional digits, trailing zeros dropped. It covers what nice
    /// steps actually produce — 0, 20, 0.001, −100, 1000000 all come out as written — and it
    /// never switches to exponent form, which would make neighbouring labels on one axis read in
    /// two different notations. A value small enough to need more than ten fractional digits
    /// renders as 0, which is wrong-looking but honest, and is a range the tick generator's own safety cap
    /// already treats as past the useful end of the scale.
    /// </remarks>
    private const string Pattern = "0.##########";

    /// <summary>
    /// Formats one tick value.
    /// </summary>
    /// <param name="value">The tick's value.</param>
    /// <param name="formatProvider">The culture to format in. Never ambient; always supplied.</param>
    /// <returns>
    /// The label text. A non-finite value yields an empty string rather than "NaN" or "∞", which
    /// are not axis labels; the generator does not produce one, so this guards a caller that
    /// bypasses it.
    /// </returns>
    public static string Format(double value, IFormatProvider formatProvider)
    {
        if (!double.IsFinite(value))
        {
            return string.Empty;
        }

        // Negative zero formats as "-0" under this pattern, which is a true statement about the
        // double and a wrong one about the axis: it sits exactly where 0 sits.
        if (value == 0)
        {
            return 0d.ToString(Pattern, formatProvider);
        }

        return value.ToString(Pattern, formatProvider);
    }

    /// <summary>The most fractional digits <see cref="Format(double, int, IFormatProvider)"/> writes: the pattern's own.</summary>
    internal const int MaximumFractionalDigits = 10;

    // "0", "0.#", "0.##" … "0.##########": the label pattern with fewer optional digits.
    private static readonly string[] Patterns = [.. Enumerable.Range(0, MaximumFractionalDigits + 1).Select(n => n == 0 ? "0" : "0." + new string('#', n))];

    /// <summary>
    /// Formats a value the way a tick value is, with at most a given number of fractional digits.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="fractionalDigits">How many fractional digits at most, 0 to <see cref="MaximumFractionalDigits"/>.</param>
    /// <param name="formatProvider">The culture to format in. Never ambient; always supplied.</param>
    /// <returns>The text: plain decimal, trailing zeros dropped, never exponent form; empty for a non-finite value.</returns>
    /// <remarks>
    /// The label pattern with its optional digits cut short, for a value that is not a nice step — the tooltip's pointer
    /// value, say, where ten digits would state far more than the screen resolves. The rules are the label's: the same
    /// culture handling, no exponent, no "-0", nothing for NaN or ∞. Only what is written is rounded; the value is not.
    /// </remarks>
    public static string Format(double value, int fractionalDigits, IFormatProvider formatProvider)
    {
        if (!double.IsFinite(value))
        {
            return string.Empty;
        }

        var pattern = Patterns[Math.Clamp(fractionalDigits, 0, MaximumFractionalDigits)];
        var text = value.ToString(pattern, formatProvider);

        // A value that rounds to zero at this many digits — -0.0001 at two, say — writes "-0", as negative zero does.
        return value == 0 || IsNegativeZeroText(text, formatProvider)
            ? 0d.ToString(pattern, formatProvider)
            : text;
    }

    /// <summary>Whether a formatted value reads as a signed zero: the culture's negative sign and nothing but zeros.</summary>
    private static bool IsNegativeZeroText(string text, IFormatProvider formatProvider)
    {
        var sign = NumberFormatInfo.GetInstance(formatProvider).NegativeSign;

        return text.StartsWith(sign, StringComparison.Ordinal) && text.AsSpan(sign.Length).IndexOfAnyExcept('0') < 0;
    }
}
