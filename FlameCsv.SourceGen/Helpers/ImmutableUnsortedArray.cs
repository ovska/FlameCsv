using System.Collections;
using System.Runtime.CompilerServices;

namespace FlameCsv.SourceGen.Helpers;

/// <summary>
/// Provides an immutable list implementation which implements sequence equality.
/// </summary>
[CollectionBuilder(typeof(ImmutableUnsortedArray), nameof(ImmutableUnsortedArray.Create))]
public sealed class ImmutableUnsortedArray<T> : IEquatable<ImmutableUnsortedArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    public ReadOnlySpan<T> AsSpan() => _values;

    public static ImmutableUnsortedArray<T> Empty { get; } = new([]);

    private readonly T[] _values;
    public T this[int index] => _values[index];
    public int Count => _values.Length;

    public ImmutableUnsortedArray(IEnumerable<T> values)
    {
        _values = values.ToArray();
    }

    public ImmutableUnsortedArray(ReadOnlySpan<T> values)
    {
        _values = values.ToArray();
    }

    public bool Equals(ImmutableUnsortedArray<T>? other)
        => other != null && ((ReadOnlySpan<T>)_values).SequenceEqual(other._values);

    public override bool Equals(object? obj) => Equals(obj as ImmutableUnsortedArray<T>);

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
internal static class ImmutableUnsortedArray
{
    public static ImmutableUnsortedArray<T> ToImmutableUnsortedArray<T>(this IEnumerable<T> values)
        where T : IEquatable<T>
    {
        if (values is ImmutableUnsortedArray<T> array)
            return array;

        return CreateRange(values);
    }

    public static ImmutableUnsortedArray<T> Create<T>() where T : IEquatable<T>
        => ImmutableUnsortedArray<T>.Empty;

    public static ImmutableUnsortedArray<T> Create<T>(T item) where T : IEquatable<T> => [item];

    public static ImmutableUnsortedArray<T> CreateRange<T>(IEnumerable<T> items) where T : IEquatable<T>
    {
        if (items is ICollection { Count: 0 })
            return ImmutableUnsortedArray<T>.Empty;

        return new(items);
    }

    public static ImmutableUnsortedArray<T> Create<T>(params T[] items) where T : IEquatable<T>
    {
        return [..(ReadOnlySpan<T>)items];
    }

    public static ImmutableUnsortedArray<T> Create<T>(ReadOnlySpan<T> items) where T : IEquatable<T>
    {
        if (items.IsEmpty)
            return ImmutableUnsortedArray<T>.Empty;

        return new(items);
    }

    public static bool Exists<T>(this ImmutableUnsortedArray<T> array, Predicate<T> match)
        where T : IEquatable<T>
    {
        foreach (T item in array)
        {
            if (match(item))
                return true;
        }

        return false;
    }
}
