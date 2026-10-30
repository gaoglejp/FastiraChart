namespace FastiraChart.Series;

/// <summary>
/// The storage a <see cref="LineSeries"/> appends into: one array with room to spare, grown geometrically.
/// </summary>
/// <remarks>
/// <para>
/// Growable point storage instead of a new array per update. An append writes one element after the last and
/// copies nothing, unless the array is full; then the array is replaced by one at least twice the size, and the
/// points are copied once. Over n appends that is fewer than 2n points copied in all — an amortized constant per
/// append, where an array per update would copy every point on every append.
/// </para>
/// <para>
/// <b>Never written below its count.</b> What <see cref="LineSeries.Points"/> reads back is a
/// <see cref="SeriesPointList"/> window onto the first <see cref="Count"/> elements of this array. Every write
/// goes after the current count, and the count never falls: <see cref="Clear"/> lets go of the array instead of
/// reusing it. So no element any window covers is ever written again, and a window handed out — read by a
/// caller, held by a clone, or set on another series — keeps its points whatever this buffer does next.
/// </para>
/// <para>
/// <b>Holds the series' own points only.</b> A list the caller set on <see cref="LineSeries.Points"/> is never
/// written into or kept here; the first append after it copies its content, as it is at that call, into a new
/// array (<see cref="Load"/>). That copy happens once per list the caller sets, not once per append.
/// </para>
/// <para>
/// Not a ring buffer and not bounded: nothing is ever dropped from the front, as nothing in the public API asks
/// for that. Internal, and not a public API: the counters on it are read-only instrumentation for
/// internal validation.
/// </para>
/// </remarks>
internal sealed class SeriesPointBuffer
{
    /// <summary>The capacity of the first array, so the first few appends do not each grow it.</summary>
    internal const int MinimumCapacity = 16;

    private ChartPoint[] _items = [];
    private int _count;

    /// <summary>How many points the buffer holds.</summary>
    public int Count => _count;

    /// <summary>How many points fit before the array must grow.</summary>
    public int Capacity => _items.Length;

    /// <summary>How many times the array has been replaced by a larger one because it was full.</summary>
    internal int Grows { get; private set; }

    /// <summary>How many points have been copied from a full array into its larger replacement, in all.</summary>
    internal long GrowthCopies { get; private set; }

    /// <summary>How many times a list not this buffer's own has been copied in (<see cref="Load"/>).</summary>
    internal int Loads { get; private set; }

    /// <summary>How many points <see cref="Load"/> has copied, in all.</summary>
    internal long LoadedPoints { get; private set; }

    /// <summary>
    /// Whether a list is this buffer's current window: the very window <see cref="Window"/> last returned, or
    /// one equal to it.
    /// </summary>
    /// <remarks>
    /// Only then may an append write straight after it. Any other list — the caller's, another series'
    /// window, an older window of this one, or one held by a clone — is copied in first, by <see cref="Load"/>.
    /// </remarks>
    public bool IsCurrent(IReadOnlyList<ChartPoint> list)
        => list is SeriesPointList window && _count > 0 && window.Count == _count && ReferenceEquals(window.Array, _items);

    /// <summary>A read-only window onto the points the buffer holds now.</summary>
    /// <remarks>
    /// New on every call, so setting it on <see cref="LineSeries.PointsProperty"/> is always a change and always
    /// notifies. One small object; the points are not copied.
    /// </remarks>
    public SeriesPointList Window() => new(_items, _count);

    /// <summary>
    /// Replaces the content with a copy of <paramref name="source"/>, with room for <paramref name="adding"/>
    /// more points after it.
    /// </summary>
    /// <remarks>
    /// Always a new array, never the current one, so that no window onto the current one changes. Sized as if
    /// the source had been appended here point by point — at least twice its count — so the appends that
    /// follow do not grow it again at once.
    /// </remarks>
    public void Load(IReadOnlyList<ChartPoint> source, int adding)
    {
        var count = source.Count;
        var items = new ChartPoint[GrownCapacity(count, checked(count + adding))];

        SeriesPointList.Copy(source, count, items);

        _items = items;
        _count = count;
        Loads++;
        LoadedPoints += count;
    }

    /// <summary>Adds one point after the last, growing the array only if it is full.</summary>
    public void Add(ChartPoint point)
    {
        if (_count == _items.Length)
        {
            Grow(checked(_count + 1));
        }

        _items[_count] = point;
        _count++;
    }

    /// <summary>Adds the first <paramref name="added"/> points of a list after the last, in one block.</summary>
    /// <remarks>
    /// Capacity is made for the whole range at once, and the range copied as one block, so a range of n points
    /// grows the array at most once and checks its capacity once. The list may be a window onto this very
    /// buffer: the copy reads the window's array, which growing leaves untouched, and writes only after the
    /// count, where no window reaches. If the list throws while it is read, the count is not advanced and the
    /// buffer holds what it held before.
    /// </remarks>
    public void AddRange(IReadOnlyList<ChartPoint> points, int added)
    {
        var needed = checked(_count + added);

        if (needed > _items.Length)
        {
            Grow(needed);
        }

        SeriesPointList.Copy(points, added, _items.AsSpan(_count, added));
        _count = needed;
    }

    /// <summary>Lets go of every point, in constant time.</summary>
    /// <remarks>
    /// The array is dropped, not emptied or reused: windows onto it may still be held, and must keep their
    /// points. The next append starts a new one.
    /// </remarks>
    public void Clear()
    {
        _items = [];
        _count = 0;
    }

    /// <summary>The capacity for <paramref name="needed"/> points, grown from <paramref name="current"/>.</summary>
    /// <remarks>Doubling, as <see cref="List{T}"/> does, with a floor and with the largest array allowed as its ceiling.</remarks>
    private static int GrownCapacity(int current, int needed)
    {
        var capacity = Math.Max((long)current * 2, MinimumCapacity);

        capacity = Math.Min(capacity, System.Array.MaxLength);

        return (int)Math.Max(capacity, needed);
    }

    /// <summary>Replaces a full array with a larger one, copying the points once.</summary>
    private void Grow(int needed)
    {
        var items = new ChartPoint[GrownCapacity(_items.Length, needed)];

        _items.AsSpan(0, _count).CopyTo(items);
        GrowthCopies += _count;
        Grows++;
        _items = items;
    }
}
