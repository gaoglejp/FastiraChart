using System.Collections;
using System.Runtime.InteropServices;

namespace FastiraChart.Series;

/// <summary>
/// The points a <see cref="LineSeries"/> holds after an append: read-only, and the series' own.
/// </summary>
/// <remarks>
/// <para>
/// What <see cref="LineSeries.Points"/> reads back once the series, rather than the caller, owns its points. It
/// is read-only so that the series' points cannot be changed behind its back, which an array handed out would
/// allow. Internal: callers see it only as an <see cref="IReadOnlyList{T}"/>, so its storage can change
/// without the public API changing.
/// </para>
/// <para>
/// <b>Storage: a window onto the first <see cref="Count"/> elements of a
/// <see cref="SeriesPointBuffer"/>'s array.</b> One append writes one element past the end of the window and
/// takes a new window one longer, over the same array; the old window does not see the new element, because
/// its count is its own. No element a window covers is ever written again — see
/// <see cref="SeriesPointBuffer"/> — so a list read before an append still reads the same points after it.
/// That is a property of this implementation, not of the contract; see <see cref="LineSeries.Points"/>.
/// </para>
/// </remarks>
internal sealed class SeriesPointList : IReadOnlyList<ChartPoint>
{
    private readonly ChartPoint[] _points;
    private readonly int _count;

    /// <summary>A window onto the first <paramref name="count"/> elements of an array the series owns.</summary>
    internal SeriesPointList(ChartPoint[] points, int count)
    {
        _points = points;
        _count = count;
    }

    /// <inheritdoc />
    public int Count => _count;

    /// <inheritdoc />
    public ChartPoint this[int index]
        => (uint)index < (uint)_count ? _points[index] : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>The points, for copying out without an enumerator.</summary>
    internal ReadOnlySpan<ChartPoint> Span => _points.AsSpan(0, _count);

    /// <summary>The array this window is onto, so the buffer can tell its own current window from any other.</summary>
    internal ChartPoint[] Array => _points;

    /// <inheritdoc />
    public IEnumerator<ChartPoint> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return _points[i];
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Copies the first <paramref name="count"/> points of any list into <paramref name="destination"/>.
    /// </summary>
    /// <remarks>
    /// Arrays, lists and this type are copied as one block; any other list is read by index. The count is the
    /// one the caller read once, so a list whose count changes while it is read cannot overrun the array.
    /// </remarks>
    internal static void Copy(IReadOnlyList<ChartPoint> source, int count, Span<ChartPoint> destination)
    {
        switch (source)
        {
            case ChartPoint[] array:
                array.AsSpan(0, count).CopyTo(destination);
                break;
            case SeriesPointList list:
                list.Span[..count].CopyTo(destination);
                break;
            case List<ChartPoint> list:
                CollectionsMarshal.AsSpan(list)[..count].CopyTo(destination);
                break;
            default:
                for (var i = 0; i < count; i++)
                {
                    destination[i] = source[i];
                }

                break;
        }
    }
}
