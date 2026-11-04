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
/// <typeparam name="T">The compared type.</typeparam>
/// <remarks>The key should be an immutable value (or a type with value equality): an object whose key changes while it sits in a hash set is lost.</remarks>
public sealed class GenericComparer<T> : IEqualityComparer<T>
    where T : class
{
    private readonly Func<T, object?> _keySelector;

    /// <summary>Creates a comparer that considers two objects equal when their keys are equal.</summary>
    /// <param name="keySelector">Selects the key two objects are compared on. Keys are compared with <see cref="object.Equals(object?, object?)"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keySelector"/> is <see langword="null"/>.</exception>
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
/// <typeparam name="T">The compared type.</typeparam>
/// <example>
/// <code>
/// var comparer = new FuncComparer&lt;User&gt;((a, b) => string.Equals(a.Email, b.Email, StringComparison.OrdinalIgnoreCase),
///                                       u => StringComparer.OrdinalIgnoreCase.GetHashCode(u.Email));
/// </code>
/// </example>
public sealed class FuncComparer<T> : IEqualityComparer<T>
    where T : class
{
    private readonly Func<T, T, bool> _equals;
    private readonly Func<T, int>? _hash;

    /// <summary>Creates a comparer from an equality predicate and an optional hash function.</summary>
    /// <param name="equals">The equality predicate. Not called when an argument is <see langword="null"/> or both are the same reference.</param>
    /// <param name="hash">Optional hash function; equal objects must produce equal hashes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="equals"/> is <see langword="null"/>.</exception>
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
