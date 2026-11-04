namespace SFerrand.Common.Collections;

/// <summary>
/// Bulk operations for <see cref="IList{T}"/> (the interface has none, unlike <see cref="List{T}"/>).
/// </summary>
public static class ListExtensions
{
    /// <summary>Appends every item of <paramref name="items"/> to the list.</summary>
    /// <typeparam name="T">Type of the items.</typeparam>
    /// <param name="list">The list to extend.</param>
    /// <param name="items">The items to append; may be the list itself.</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> or <paramref name="items"/> is <see langword="null"/>.</exception>
    public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(items);

        // Materialize first: `list.AddRange(list)` must not enumerate a list being modified.
        foreach (var item in items.ToArray())
            list.Add(item);
    }

    /// <summary>Removes every item matching <paramref name="match"/> in a single pass.</summary>
    /// <typeparam name="T">Type of the items.</typeparam>
    /// <param name="list">The list to filter.</param>
    /// <param name="match">The predicate selecting the items to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> or <paramref name="match"/> is <see langword="null"/>.</exception>
    /// <remarks>Walks backwards so indexes stay valid while removing. On a plain <c>List&lt;T&gt;</c> prefer its own, faster <c>RemoveAll</c> (instance methods win overload resolution).</remarks>
    /// <returns>The number of removed items.</returns>
    public static int RemoveAll<T>(this IList<T> list, Func<T, bool> match)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(match);

        var removed = 0;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!match(list[i]))
                continue;

            list.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    /// <summary>Removes the first occurrence of each of <paramref name="items"/> from the list.</summary>
    /// <typeparam name="T">Type of the items.</typeparam>
    /// <param name="list">The list to shrink.</param>
    /// <param name="items">The items to remove; items absent from the list are ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> or <paramref name="items"/> is <see langword="null"/>.</exception>
    /// <returns>The number of removed items.</returns>
    public static int RemoveRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(items);

        return items.ToArray().Count(list.Remove);
    }
}
