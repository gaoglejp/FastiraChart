using System.Collections;

namespace FastiraChart.Rendering;

/// <summary>
/// Two read-only lists read as one, first then second, without copying either.
/// </summary>
/// <remarks>
/// A layer whose geometry is kept as two parts — one per value axis — composes its frame from whichever
/// parts still stand, and a reader that wants the layer's whole list (internal validation, which reads every tick
/// mark or label in drawing order) is handed this instead of a copy. The parts are the control's own cached lists and
/// are never changed after they are made, so the view is as stable as they are.
/// </remarks>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class JoinedList<T>(IReadOnlyList<T> first, IReadOnlyList<T> second) : IReadOnlyList<T>
{
    /// <summary>The first part.</summary>
    public IReadOnlyList<T> First => first;

    /// <summary>The second part.</summary>
    public IReadOnlyList<T> Second => second;

    /// <inheritdoc />
    public int Count => first.Count + second.Count;

    /// <inheritdoc />
    public T this[int index] => index < first.Count ? first[index] : second[index - first.Count];

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in first)
        {
            yield return item;
        }

        foreach (var item in second)
        {
            yield return item;
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The two parts read as one: <paramref name="previous"/> itself when it already joins these very parts, so a frame
    /// composed again from parts that did not change hands its readers the same list it did before.
    /// </summary>
    /// <param name="first">The first part.</param>
    /// <param name="second">The second part.</param>
    /// <param name="previous">What the last composition handed out, or null.</param>
    public static IReadOnlyList<T> Of(IReadOnlyList<T> first, IReadOnlyList<T> second, IReadOnlyList<T>? previous)
    {
        if (second.Count == 0)
        {
            return first;
        }

        return previous is JoinedList<T> joined && ReferenceEquals(joined.First, first) && ReferenceEquals(joined.Second, second)
            ? joined
            : new JoinedList<T>(first, second);
    }
}
