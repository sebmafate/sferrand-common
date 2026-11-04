namespace SFerrand.Common.Collections;

/// <summary>
/// Bulk operations for <see cref="IList{T}"/> (the interface has none, unlike <see cref="List{T}"/>).
/// </summary>
public static class ListExtensions
{
    /// <summary>Appends every item of <paramref name="items"/> to the list.</summary>
    public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(items);

        // Materialize first: `list.AddRange(list)` must not enumerate a list being modified.
        foreach (var item in items.ToArray())
            list.Add(item);
    }

    /// <summary>Removes every item matching <paramref name="match"/> in a single pass.</summary>
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
    /// <returns>The number of removed items.</returns>
    public static int RemoveRange<T>(this IList<T> list, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(items);

        return items.ToArray().Count(list.Remove);
    }
}
