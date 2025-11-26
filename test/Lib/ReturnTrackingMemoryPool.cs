using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FlameCsv.IO;
using FlameCsv.IO.Internal;

namespace FlameCsv.Tests;

public sealed class ReturnTrackingBufferPool : IBufferPool, IDisposable
{
    public readonly ReturnTrackingMemoryPool<byte> _bytePool;
    public readonly ReturnTrackingMemoryPool<char> _charPool;

    public ReturnTrackingBufferPool(bool? guardedFromEnd = null)
    {
        _bytePool = ReturnTrackingMemoryPool<byte>.Create(guardedFromEnd);
        _charPool = ReturnTrackingMemoryPool<char>.Create(guardedFromEnd);

        _bytePool.TrackStackTraces = true;
        _charPool.TrackStackTraces = true;
    }

    public IMemoryOwner<byte> GetBytes(int length) => _bytePool.Rent(length);

    public IMemoryOwner<char> GetChars(int length) => _charPool.Rent(length);

    public void Dispose()
    {
        using (_bytePool)
        {
            using (_charPool) { }
        }
    }
}

[SupportedOSPlatform("windows")]
file sealed class ReturnTrackingGuardedMemoryPool<T>(bool fromEnd) : ReturnTrackingMemoryPool<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public override int MaxBufferSize { get; } = Environment.SystemPageSize * 128;

    protected override Memory<T> Initialize(int length)
    {
        var manager = new GuardedMemoryManager<T>(length, fromEnd);
        return manager.Memory;
    }

    protected override bool TryRelease(Memory<T> memory)
    {
        if (MemoryMarshal.TryGetMemoryManager<T, GuardedMemoryManager<T>>(memory, out var manager))
        {
            ((IDisposable)manager).Dispose();
            return true;
        }

        return false;
    }
}

file sealed class ReturnTrackingArrayMemoryPool<T> : ReturnTrackingMemoryPool<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public override int MaxBufferSize => Array.MaxLength;

    private const int Boundaries = 128;

    protected override Memory<T> Initialize(int length)
    {
        T[] array = ArrayPool<T>.Shared.Rent(length + Boundaries * 2);

        array.AsSpan().Fill(T.AllBitsSet);

        return array.AsMemory(Boundaries, length);
    }

    protected override bool TryRelease(Memory<T> memory)
    {
        if (MemoryMarshal.TryGetArray<T>(memory, out var segment))
        {
            T[] array = segment.Array!;

            if (
                array.AsSpan(..Boundaries).ContainsAnyExcept(T.AllBitsSet)
                || array.AsSpan(Boundaries + memory.Length).ContainsAnyExcept(T.AllBitsSet)
            )
            {
                throw new InvalidOperationException($"OOB write detected");
            }

            ArrayPool<T>.Shared.Return(segment.Array!);
            return true;
        }

        return false;
    }
}

public abstract class ReturnTrackingMemoryPool<T> : MemoryPool<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public static ReturnTrackingMemoryPool<T> Create(bool? guardedFromEnd = null)
    {
        return guardedFromEnd switch
        {
            { } b when OperatingSystem.IsWindows() => new ReturnTrackingGuardedMemoryPool<T>(b),
            not null => throw new NotSupportedException("Guarded memory is only supported on Windows."),
            _ => new ReturnTrackingArrayMemoryPool<T>(),
        };
    }

    public bool TrackStackTraces { get; set; }

    private readonly ConcurrentDictionary<Owner, StackTrace?> _values = new(ReferenceEqualityComparer.Instance);
    private int _rentedCount;
    private int _returnedCount;

    protected abstract Memory<T> Initialize(int length);
    protected abstract bool TryRelease(Memory<T> memory);

    public sealed class Owner(ReturnTrackingMemoryPool<T> pool, int minimumLength) : IMemoryOwner<T>
    {
        private bool _disposed;

        public Memory<T> Memory { get; private set; } = pool.Initialize(minimumLength);

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                pool.Return(this);
            }
            finally
            {
                Memory = default;
            }
        }
    }

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minBufferSize, MaxBufferSize);

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
                $"{_values.Count} rented memory not disposed, {_returnedCount} out of {_rentedCount}. "
                    + Environment.NewLine
                    + string.Join(Environment.NewLine + Environment.NewLine, _values.Select(kvp => kvp.Value))
            );
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

        var memory = instance.Memory;

        if (!TryRelease(memory))
        {
            throw new InvalidOperationException(
                $"Memory<{typeof(T)}>[{memory.Length}] was not from {GetType().FullName}"
            );
        }
    }
}
