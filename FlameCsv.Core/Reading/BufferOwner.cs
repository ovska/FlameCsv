﻿using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading;

/// <summary>
/// Wrapper around an array reference and the array pool that owns it.
/// </summary>
[DebuggerDisplay(@"\{ ValueBufferOwner: Length: {_span[0] != null ? _span[0].Length.ToString() : ""-1"",nq} \}")]
internal readonly ref struct BufferOwner<T> where T : unmanaged
{
    private readonly Span<T[]?> _span;
    private readonly ArrayPool<T> _pool;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BufferOwner(ref T[]? span, ArrayPool<T> pool)
    {
        _span = MemoryMarshal.CreateSpan(ref span, 1);
        _pool = pool;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int length)
    {
        ref T[]? array = ref _span[0];
        _pool.EnsureCapacity(ref array, length);
        return array.AsSpan(0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int length)
    {
        ref T[]? array = ref _span[0];
        _pool.EnsureCapacity(ref array, length);
        return array.AsMemory(0, length);
    }
}