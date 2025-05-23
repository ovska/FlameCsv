﻿using System.Runtime.CompilerServices;

namespace FlameCsv.Writing.Escaping;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct RFC4180Escaper<T>(T quote) : IEscaper<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public T Quote
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => quote;
    }

    public T Escape
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => quote;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsEscaping(T value) => value == quote;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastIndexOfEscapable(scoped ReadOnlySpan<T> value) => value.LastIndexOf(quote);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountEscapable(scoped ReadOnlySpan<T> value) => value.Count(quote);
}
