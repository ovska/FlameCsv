using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlameCsv.IO;
using FlameCsv.IO.Internal;

namespace FlameCsv.Tests;

public sealed class ReturnTrackingBufferPool : IBufferPool, IDisposable
{
    public readonly ReturnTrackingMemoryPool<byte> _bytePool;
    public readonly ReturnTrackingMemoryPool<char> _charPool;

    public ReturnTrackingBufferPool(PoisonPagePlacement placement = PoisonPagePlacement.None)
    {
        _bytePool = ReturnTrackingMemoryPool<byte>.Create(placement);
        _charPool = ReturnTrackingMemoryPool<char>.Create(placement);
        // _bytePool.TrackStackTraces = true;
        // _charPool.TrackStackTraces = true;
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

file sealed class ReturnTrackingGuardedMemoryPool<T>(PoisonPagePlacement placement) : ReturnTrackingMemoryPool<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public override int MaxBufferSize { get; } = Environment.SystemPageSize * 128;

    protected override Memory<T> Initialize(int length)
    {
        var manager = BoundedMemory.Allocate<T>(length, placement);
        return manager.Memory;
    }

    protected override bool TryRelease(Memory<T> memory)
    {
        if (MemoryMarshal.TryGetMemoryManager<T, BoundedMemory.Manager<T>>(memory, out var manager))
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
    public static ReturnTrackingMemoryPool<T> Create(PoisonPagePlacement placement)
    {
        return placement switch
        {
            not PoisonPagePlacement.None => new ReturnTrackingGuardedMemoryPool<T>(placement),
            _ => new ReturnTrackingArrayMemoryPool<T>(),
        };
    }

    public bool TrackStackTraces { get; set; }

    private readonly ConcurrentDictionary<Owner, (int index, StackTrace? stackTrace)> _values = new(
        ReferenceEqualityComparer.Instance
    );
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
            if (Interlocked.Exchange(ref _disposed, true))
            {
                throw new Exception("Memory disposed twice");
            }

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
        int index = Interlocked.Increment(ref _rentedCount);
        _values.TryAdd(owner, (index, TrackStackTraces ? new StackTrace(fNeedFileInfo: true) : null));
        return owner;
    }

    protected override void Dispose(bool disposing)
    {
        var sw = new SpinWait();

        for (int i = 0; i < 100; i++)
        {
            if (Volatile.Read(ref _rentedCount) == Volatile.Read(ref _returnedCount) && _values.IsEmpty)
            {
                break;
            }

            sw.SpinOnce();
        }

        int rented = Volatile.Read(ref _rentedCount);
        int returned = Volatile.Read(ref _returnedCount);

        if (!_values.IsEmpty || rented != returned)
        {
            throw new InvalidOperationException(
                $"{_values.Count} rented memory not disposed out of {rented}. "
                    + $"Returned: {returned}."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine + Environment.NewLine,
                        _values.Select(kvp => $"IDX: {kvp.Value.index}{Environment.NewLine}{kvp.Value.stackTrace}")
                    )
            );
        }
    }

    internal void Return(Owner instance)
    {
        if (instance.Memory.Length == 0 || !_values.TryRemove(instance, out _))
        {
            throw new InvalidOperationException(
                $"The returned memory was not rented from the pool (length: {instance.Memory.Length})."
            );
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
