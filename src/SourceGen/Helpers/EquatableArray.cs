// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

// ReSharper disable all

namespace FlameCsv.SourceGen.Helpers;

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
        return ImmutableCollectionsMarshal.AsImmutableArray(items);
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
