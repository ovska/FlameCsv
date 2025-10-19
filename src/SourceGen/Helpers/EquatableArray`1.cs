﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

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
        _array = ImmutableCollectionsMarshal.AsArray(array);
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

    /// <inheritdoc/>
    public bool Equals(EquatableArray<T> array)
    {
        return AsSpan().SequenceEqual(array.AsSpan());
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is EquatableArray<T> array && Equals(array);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (_array is not T[] { Length: > 0 } array)
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
        return ImmutableCollectionsMarshal.AsImmutableArray(_array);
    }

    public T[]? UnsafeArray => _array;

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
    /// Gets an <see cref="ImmutableArray{T}.Enumerator"/> value to traverse items in the current array.
    /// </summary>
    /// <returns>An <see cref="ImmutableArray{T}.Enumerator"/> value to traverse items in the current array.</returns>
    public Enumerator GetEnumerator()
    {
        return new(_array);
    }

    /// <summary>
    /// Gets an <see cref="WhereEnumerable"/> value to traverse items in the current array that match a given predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match items against.</param>
    /// <returns></returns>
    public WhereEnumerable Where(Func<T, bool> predicate)
    {
        return new(_array, predicate);
    }

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
    }

    /// <inheritdoc/>
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

    public struct Enumerator(T[]? array)
    {
        private readonly T[] _array = array ?? [];
        private int _index = -1;

        public readonly ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _array[_index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_index < _array.Length;
    }

    public readonly struct WhereEnumerable(T[]? array, Func<T, bool> predicate)
    {
        public WhereEnumerator GetEnumerator()
        {
            return new(array, predicate);
        }
    }

    public struct WhereEnumerator(T[]? array, Func<T, bool> predicate)
    {
        private readonly T[] _array = array ?? [];
        private readonly Func<T, bool> _predicate = predicate;
        private int _index = -1;

        public readonly ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _array[_index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (++_index < _array.Length)
            {
                if (_predicate(_array[_index]))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [ExcludeFromCodeCoverage]
    public override string ToString() => $"EquatableArray<{typeof(T).Name}>[{Length}]";

    [ExcludeFromCodeCoverage]
    internal sealed class DebuggerTypeProxy
    {
        public DebuggerTypeProxy(EquatableArray<T> array)
        {
            Items = [.. array.ToImmutableArray()];
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items { get; }
    }
}
