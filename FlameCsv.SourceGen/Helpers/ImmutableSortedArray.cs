using System.Collections;
using System.Runtime.CompilerServices;

namespace FlameCsv.SourceGen.Helpers;

/// <summary>
/// Provides an immutable list implementation which implements sequence equality.
/// </summary>
[CollectionBuilder(typeof(ImmutableSortedArray), nameof(ImmutableSortedArray.Create))]
public sealed class ImmutableSortedArray<T> : IEquatable<ImmutableSortedArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>, IComparable<T>
{
    public ReadOnlySpan<T> AsSpan() => _values;

    public static ImmutableSortedArray<T> Empty { get; } = new([]);

    private readonly T[] _values;
    public T this[int index] => _values[index];
    public int Count => _values.Length;

    public ImmutableSortedArray(IEnumerable<T> values)
    {
        _values = values.ToArray();
        Array.Sort(_values);
    }

    public ImmutableSortedArray(ReadOnlySpan<T> values)
    {
        _values = values.ToArray();
        Array.Sort(_values);
    }

    public bool Equals(ImmutableSortedArray<T>? other)
        => other != null && ((ReadOnlySpan<T>)_values).SequenceEqual(other._values);

    public override bool Equals(object? obj) => Equals(obj as ImmutableSortedArray<T>);

    public override int GetHashCode()
    {
        int hash = 17;

        foreach (T value in _values)
        {
            hash = (hash * 397) ^ (value?.GetHashCode() ?? 0);
        }

        return hash;
    }

    public Enumerator GetEnumerator() => new Enumerator(_values);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)_values).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

    public struct Enumerator
    {
        private readonly T[] _values;
        private int _index;

        internal Enumerator(T[] values)
        {
            _values = values;
            _index = -1;
        }

        public bool MoveNext()
        {
            int newIndex = _index + 1;

            if ((uint)newIndex < (uint)_values.Length)
            {
                _index = newIndex;
                return true;
            }

            return false;
        }

        public readonly T Current => _values[_index];
    }
}

[SuppressMessage("Style", "IDE0301:Simplify collection initialization")]
internal static class ImmutableSortedArray
{
    public static ImmutableSortedArray<T> ToImmutableSortedArray<T>(this IEnumerable<T> values) where T : IEquatable<T>, IComparable<T>
    {
        if (values is ImmutableSortedArray<T> array)
            return array;

        return CreateRange(values);
    }

    public static ImmutableSortedArray<T> Create<T>() where T : IEquatable<T>, IComparable<T> => ImmutableSortedArray<T>.Empty;

    public static ImmutableSortedArray<T> Create<T>(T item) where T : IEquatable<T>, IComparable<T> => [item];

    public static ImmutableSortedArray<T> CreateRange<T>(IEnumerable<T> items) where T : IEquatable<T>, IComparable<T>
    {
        if (items is ICollection { Count: 0 })
            return ImmutableSortedArray<T>.Empty;

        return new(items);
    }

    public static ImmutableSortedArray<T> Create<T>(params T[] items) where T : IEquatable<T>, IComparable<T>
    {
        // ReSharper disable once UseCollectionExpression
        return Create((ReadOnlySpan<T>)items);
    }

    public static ImmutableSortedArray<T> Create<T>(ReadOnlySpan<T> items) where T : IEquatable<T>, IComparable<T>
    {
        if (items.IsEmpty)
            return ImmutableSortedArray<T>.Empty;

        return new(items);
    }

    public static bool Exists<T>(this ImmutableSortedArray<T> array, Predicate<T> match)
        where T : IEquatable<T>, IComparable<T>
    {
        foreach (T item in array)
        {
            if (match(item))
                return true;
        }

        return false;
    }
}
