﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// ReSharper disable all

namespace FlameCsv.SourceGen.Helpers;

/// <summary>
/// An immutable, equatable array. This is equivalent to <see cref="ImmutableArray"/> but with value equality support.
/// </summary>
/// <typeparam name="T">The type of values in the array.</typeparam>
[CollectionBuilder(typeof(EquatableArray), nameof(EquatableArray.Create))]
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(ImmutableArray<T>.Empty);

    /// <summary>
    /// The underlying <typeparamref name="T"/> array.
    /// </summary>
    private readonly T[]? _array;

    /// <summary>
    /// Creates a new <see cref="EquatableArray{T}"/> instance.
    /// </summary>
    /// <param name="array">The input <see cref="ImmutableArray{T}"/> to wrap.</param>
    [OverloadResolutionPriority(-1)]
    public EquatableArray(ImmutableArray<T> array)
    {
        _array = Unsafe.As<ImmutableArray<T>, T[]?>(ref array);
    }

    public EquatableArray(scoped ReadOnlySpan<T> span)
    {
        _array = span.ToArray();
    }

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array?.Length ?? 0;
    }

    /// <summary>
    /// Gets a reference to an item at a specified position within the array.
    /// </summary>
    /// <param name="index">The index of the item to retrieve a reference to.</param>
    /// <returns>A reference to an item at a specified position within the array.</returns>
    public ref readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref AsImmutableArray().ItemRef(index);
    }

    /// <summary>
    /// Gets a value indicating whether the current array is empty.
    /// </summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _array is null || _array.Length == 0;
    }

    /// <sinheritdoc/>
    public bool Equals(EquatableArray<T> array)
    {
        return AsSpan().SequenceEqual(array.AsSpan());
    }

    /// <sinheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is EquatableArray<T> array && Equals(this, array);
    }

    /// <sinheritdoc/>
    public override int GetHashCode()
    {
        if (_array is not T[] array)
        {
            return 0;
        }

        HashCode hashCode = default;

        foreach (T item in array)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Gets an <see cref="ImmutableArray{T}"/> instance from the current <see cref="EquatableArray{T}"/>.
    /// </summary>
    /// <returns>The <see cref="ImmutableArray{T}"/> from the current <see cref="EquatableArray{T}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ImmutableArray<T> AsImmutableArray()
    {
        return Unsafe.As<T[]?, ImmutableArray<T>>(ref Unsafe.AsRef(in _array));
    }

    public T[]? UnsafeGetArray => _array;

    /// <summary>
    /// Creates an <see cref="EquatableArray{T}"/> instance from a given <see cref="ImmutableArray{T}"/>.
    /// </summary>
    /// <param name="array">The input <see cref="ImmutableArray{T}"/> instance.</param>
    /// <returns>An <see cref="EquatableArray{T}"/> instance from a given <see cref="ImmutableArray{T}"/>.</returns>
    public static EquatableArray<T> FromImmutableArray(ImmutableArray<T> array)
    {
        return new(array);
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlySpan{T}"/> wrapping the current items.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{T}"/> wrapping the current items.</returns>
    public ReadOnlySpan<T> AsSpan()
    {
        return AsImmutableArray().AsSpan();
    }

    /// <summary>
    /// Copies the contents of this <see cref="EquatableArray{T}"/> instance to a mutable array.
    /// </summary>
    /// <returns>The newly instantiated array.</returns>
    public T[] ToArray()
    {
        return AsImmutableArray().ToArray();
    }

    /// <summary>
    /// Gets an <see cref="ImmutableArray{T}.Enumerator"/> value to traverse items in the current array.
    /// </summary>
    /// <returns>An <see cref="ImmutableArray{T}.Enumerator"/> value to traverse items in the current array.</returns>
    public Enumerator GetEnumerator()
    {
        return new(_array!);
    }

    /// <sinheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
    }

    /// <sinheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)AsImmutableArray()).GetEnumerator();
    }

    /// <summary>
    /// Implicitly converts an <see cref="ImmutableArray{T}"/> to <see cref="EquatableArray{T}"/>.
    /// </summary>
    /// <returns>An <see cref="EquatableArray{T}"/> instance from a given <see cref="ImmutableArray{T}"/>.</returns>
    public static implicit operator EquatableArray<T>(ImmutableArray<T> array)
    {
        return FromImmutableArray(array);
    }

    /// <summary>
    /// Implicitly converts an <see cref="EquatableArray{T}"/> to <see cref="ImmutableArray{T}"/>.
    /// </summary>
    /// <returns>An <see cref="ImmutableArray{T}"/> instance from a given <see cref="EquatableArray{T}"/>.</returns>
    public static implicit operator ImmutableArray<T>(EquatableArray<T> array)
    {
        return array.AsImmutableArray();
    }

    /// <summary>
    /// Checks whether two <see cref="EquatableArray{T}"/> values are the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableArray{T}"/> value.</param>
    /// <param name="right">The second <see cref="EquatableArray{T}"/> value.</param>
    /// <returns>Whether <paramref name="left"/> and <paramref name="right"/> are equal.</returns>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Checks whether two <see cref="EquatableArray{T}"/> values are not the same.
    /// </summary>
    /// <param name="left">The first <see cref="EquatableArray{T}"/> value.</param>
    /// <param name="right">The second <see cref="EquatableArray{T}"/> value.</param>
    /// <returns>Whether <paramref name="left"/> and <paramref name="right"/> are not equal.</returns>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }

    public struct Enumerator(T[] array)
    {
        private readonly T[] _array = array;
        private int _index = -1;

        public readonly ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _array[_index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_index < _array.Length;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"EquatableArray<{typeof(T).Name}>[{Length}]";

    [ExcludeFromCodeCoverage]
    internal sealed class DebuggerTypeProxy
    {
        private readonly EquatableArray<T> _array;

        public DebuggerTypeProxy(EquatableArray<T> array)
        {
            _array = array;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items => _array.ToArray();
    }
}

[SuppressMessage("Style", "IDE0301:Simplify collection initialization")]
internal static class EquatableArray
{
    [Obsolete("Unnecessary ToEquatableArray call", true)]
    public static EquatableArray<T> ToEquatableArray<T>(this EquatableArray<T> values)
        where T : IEquatable<T?>
    {
        return values;
    }

    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> values)
        where T : IEquatable<T?>
    {
        if (values is ImmutableArray<T> array)
            return array;

        return CreateRange(values);
    }

    public static EquatableArray<T> Create<T>()
        where T : IEquatable<T?> => EquatableArray<T>.Empty;

    public static EquatableArray<T> Create<T>(T item)
        where T : IEquatable<T?> => [item];

    public static EquatableArray<T> CreateRange<T>(IEnumerable<T> items)
        where T : IEquatable<T?>
    {
        if (items is ICollection { Count: 0 })
            return EquatableArray<T>.Empty;

        return new(items.ToImmutableArray());
    }

    public static EquatableArray<T> Create<T>(params T[] items)
        where T : IEquatable<T?>
    {
        return Unsafe.As<T[], ImmutableArray<T>>(ref items);
    }

    public static EquatableArray<T> CreateSorted<T>(ICollection<T> items)
        where T : IEquatable<T?>, IComparable<T>
    {
        if (items is not { Count: > 0 })
            return [];

        T[] array = new T[items.Count];
        items.CopyTo(array, 0);
        Array.Sort(array);
        return Create(array);
    }

    public static EquatableArray<T> Create<T>(ReadOnlySpan<T> items)
        where T : IEquatable<T?>
    {
        if (items.IsEmpty)
            return EquatableArray<T>.Empty;

        return new(items);
    }
}
