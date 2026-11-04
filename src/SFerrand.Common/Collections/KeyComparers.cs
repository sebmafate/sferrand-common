namespace SFerrand.Common.Collections;

/// <summary>
/// Compares objects through a key selected from each of them. Usable with <c>Distinct</c>, <c>Except</c>,
/// <c>HashSet</c> and <c>Dictionary</c>, with a hash code that is consistent with equality.
/// </summary>
/// <example>
/// <code>
/// var distinctByName = users.Distinct(new GenericComparer&lt;User&gt;(u =&gt; u.Name));
/// </code>
/// </example>
public sealed class GenericComparer<T> : IEqualityComparer<T>
    where T : class
{
    private readonly Func<T, object?> _keySelector;

    /// <param name="keySelector">Selects the key two objects are compared on.</param>
    public GenericComparer(Func<T, object?> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _keySelector = keySelector;
    }

    /// <inheritdoc />
    public bool Equals(T? x, T? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return Equals(_keySelector(x), _keySelector(y));
    }

    /// <inheritdoc />
    public int GetHashCode(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return _keySelector(obj)?.GetHashCode() ?? 0;
    }
}

/// <summary>
/// Compares objects with a custom predicate.
/// </summary>
/// <remarks>
/// A hash function consistent with the predicate is required for hash-based collections and LINQ set operations
/// to be correct. Without one every object hashes to the same bucket: the results are right but the lookups are O(n).
/// </remarks>
public sealed class FuncComparer<T> : IEqualityComparer<T>
    where T : class
{
    private readonly Func<T, T, bool> _equals;
    private readonly Func<T, int>? _hash;

    /// <param name="equals">The equality predicate.</param>
    /// <param name="hash">Optional hash function; equal objects must produce equal hashes.</param>
    public FuncComparer(Func<T, T, bool> equals, Func<T, int>? hash = null)
    {
        ArgumentNullException.ThrowIfNull(equals);
        _equals = equals;
        _hash = hash;
    }

    /// <inheritdoc />
    public bool Equals(T? x, T? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return _equals(x, y);
    }

    /// <inheritdoc />
    public int GetHashCode(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return _hash?.Invoke(obj) ?? 0;
    }
}
