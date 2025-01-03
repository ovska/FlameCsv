using System.Collections;
using System.Runtime.CompilerServices;

namespace FlameCsv.SourceGen.Helpers;

/// <summary>
/// Provides an immutable list implementation which implements sequence equality.
/// </summary>
[CollectionBuilder(typeof(ImmutableEquatableArray), nameof(ImmutableEquatableArray.Create))]
public sealed class ImmutableEquatableArray<T> : IEquatable<ImmutableEquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>, IComparable<T>
{
    public static ImmutableEquatableArray<T> Empty { get; } = new([]);

    private readonly T[] _values;
    public T this[int index] => _values[index];
    public int Count => _values.Length;

    public ImmutableEquatableArray(IEnumerable<T> values)
    {
        _values = values.ToArray();
        Array.Sort(_values);
    }

    public ImmutableEquatableArray(ReadOnlySpan<T> values)
    {
        _values = values.ToArray();
        Array.Sort(_values);
    }

    public bool Equals(ImmutableEquatableArray<T>? other)
        => other != null && ((ReadOnlySpan<T>)_values).SequenceEqual(other._values);

    public override bool Equals(object? obj) => Equals(obj as ImmutableEquatableArray<T>);

    public override int GetHashCode()
    {
        int hash = 0;

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
internal static class ImmutableEquatableArray
{
    public static ImmutableEquatableArray<T> ToImmutableEquatableArray<T>(this IEnumerable<T> values) where T : IEquatable<T>, IComparable<T>
    {
        return CreateRange(values);
    }

    public static ImmutableEquatableArray<T> Create<T>() where T : IEquatable<T>, IComparable<T> => ImmutableEquatableArray<T>.Empty;

    public static ImmutableEquatableArray<T> Create<T>(T item) where T : IEquatable<T>, IComparable<T> => [item];

    public static ImmutableEquatableArray<T> CreateRange<T>(IEnumerable<T> items) where T : IEquatable<T>, IComparable<T>
    {
        if (items is ICollection { Count: 0 })
            return ImmutableEquatableArray<T>.Empty;

        return new(items);
    }

    public static ImmutableEquatableArray<T> Create<T>(params T[] items) where T : IEquatable<T>, IComparable<T>
    {
        return [..(ReadOnlySpan<T>)items];
    }

    public static ImmutableEquatableArray<T> Create<T>(ReadOnlySpan<T> items) where T : IEquatable<T>, IComparable<T>
    {
        if (items.IsEmpty)
            return ImmutableEquatableArray<T>.Empty;

        return new(items);
    }
}
