﻿using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FlameCsv.Extensions;

namespace FlameCsv.Tests.Utilities;

[SupportedOSPlatform("windows")]
internal sealed class ReturnTrackingGuardedMemoryPool<T>(bool fromEnd) : ReturnTrackingMemoryPool<T> where T : unmanaged
{
    public override int MaxBufferSize { get; } = Environment.SystemPageSize * 128;

    protected override Memory<T> Initialize(int length)
    {
        var manager = new GuardedMemoryManager<T>(length, fromEnd);
        return manager.Memory;
    }

    protected override void Release(Memory<T> memory)
    {
        if (MemoryMarshal.TryGetMemoryManager<T, GuardedMemoryManager<T>>(memory, out var manager))
        {
            ((IDisposable)manager).Dispose();
            return;
        }

        throw new InvalidOperationException("Memory was not from GuardedMemoryManager");
    }
}

internal sealed class ReturnTrackingArrayMemoryPool<T> : ReturnTrackingMemoryPool<T>
{
    public override int MaxBufferSize => Array.MaxLength;

    protected override Memory<T> Initialize(int length)
    {
        return ArrayPool<T>.Shared.Rent(length);
    }

    protected override void Release(Memory<T> memory)
    {
        if (MemoryMarshal.TryGetArray<T>(memory, out var segment))
        {
            ArrayPool<T>.Shared.Return(segment.Array!);
            return;
        }

        throw new InvalidOperationException("Memory was not from GuardedMemoryManager");
    }
}

internal abstract class ReturnTrackingMemoryPool<T> : MemoryPool<T>
{
    public bool TrackStackTraces { get; set; }

    private readonly ConcurrentDictionary<Owner, StackTrace?> _values = new(ReferenceEqualityComparer.Instance);
    private int _rentedCount;
    private int _returnedCount;

    protected abstract Memory<T> Initialize(int length);
    protected abstract void Release(Memory<T> memory);

    public sealed class Owner(ReturnTrackingMemoryPool<T> pool, int minimumLength) : IMemoryOwner<T>
    {
        private bool _disposed;

        public Memory<T> Memory { get; private set; } = pool.Initialize(minimumLength);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            pool.Return(this);
            Memory = default;
        }
    }

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        if (minBufferSize == 0)
            return HeapMemoryOwner<T>.Empty;

        if (minBufferSize == -1)
            minBufferSize = 4096;

        ArgumentOutOfRangeException.ThrowIfNegative(minBufferSize);

        var owner = new Owner(this, minBufferSize);
        _values.TryAdd(owner, TrackStackTraces ? new StackTrace(fNeedFileInfo: true) : null);
        Interlocked.Increment(ref _rentedCount);
        return owner;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_values.IsEmpty)
        {
            throw new InvalidOperationException(
                $"{_values.Count} rented memory not disposed, {_returnedCount} out of {_rentedCount}. " +
                Environment.NewLine +
                string.Join(Environment.NewLine + Environment.NewLine, _values.Select(kvp => kvp.Value)));
        }
    }

    internal void Return(Owner instance)
    {
        if (instance.Memory.Length == 0)
        {
            return;
        }

        if (!_values.TryRemove(instance, out _))
        {
            throw new InvalidOperationException("The returned memory was not rented from the pool.");
        }

        Interlocked.Increment(ref _returnedCount);
        Release(instance.Memory);
    }
}