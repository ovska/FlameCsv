using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlameCsv.IO;

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
#if FULL_TEST_SUITE
    private readonly ConcurrentStack<T[]> _releasedValues = [];
#else
    private static readonly ArrayPool<T> _arrayPool = ArrayPool<T>.Create();
#endif
    private static T Sentinel => T.AllBitsSet;

    public override int MaxBufferSize => Array.MaxLength;

    private const int Boundaries = 256;

    protected override Memory<T> Initialize(int length)
    {
#if FULL_TEST_SUITE
        T[] array = GC.AllocateUninitializedArray<T>(length + Boundaries * 2);
#else
        T[] array = _arrayPool.Rent(length + Boundaries * 2);
#endif
        array.AsSpan(..Boundaries).Fill(Sentinel);
        array.AsSpan(Boundaries, length).Fill(T.AllBitsSet - T.One); // fill usable range with a different pattern
        array.AsSpan(Boundaries + length).Fill(Sentinel);
        return array.AsMemory(Boundaries, length);
    }

    protected override bool TryRelease(Memory<T> memory)
    {
        if (MemoryMarshal.TryGetArray<T>(memory, out var segment))
        {
            T[] array = segment.Array!;
            bool before = array.AsSpan(..Boundaries).ContainsAnyExcept(Sentinel);
            bool after = array.AsSpan(Boundaries + memory.Length).ContainsAnyExcept(Sentinel);

            if (before || after)
            {
                throw new InvalidOperationException($"OOB write detected: before={before}, after={after}");
            }

#if FULL_TEST_SUITE
            array.AsSpan().Fill(T.CreateTruncating(0xBEBEBEBE)); // poison the array
            _releasedValues.Push(array);
#else
            _arrayPool.Return(segment.Array!);
#endif
            return true;
        }

        return false;
    }

#if FULL_TEST_SUITE
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        while (_releasedValues.TryPop(out var array))
        {
            if (array.ContainsAnyExcept(T.CreateTruncating(0xBEBEBEBE)))
            {
                throw new InvalidOperationException(
                    $"Use after free detected on array with length {array.Length}: {array.AsSpan().AsPrintableString()}"
                );
            }
        }
    }
#endif
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

    public sealed class Owner : IMemoryOwner<T>
    {
        private readonly ReturnTrackingMemoryPool<T> _pool;
        internal readonly Memory<T> _memory;
        private bool _disposed;

        public Owner(ReturnTrackingMemoryPool<T> pool, int minimumLength)
        {
            _pool = pool;
            _memory = pool.Initialize(minimumLength);
        }

        public Memory<T> Memory
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _memory;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, true))
            {
                throw new Exception("Memory disposed twice");
            }

            _pool.Return(this);
        }
    }

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minBufferSize, MaxBufferSize);

        if (minBufferSize == 0)
            return MemoryPool<T>.Shared.Rent(0);

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
        SpinWait.SpinUntil(
            () =>
            {
                int rented = Volatile.Read(ref _rentedCount);
                int returned = Volatile.Read(ref _returnedCount);
                return _values.IsEmpty && rented == returned;
            },
            millisecondsTimeout: 500
        );

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
                        _values.Select(kvp => $"Index: {kvp.Value.index}{Environment.NewLine}{kvp.Value.stackTrace}")
                    )
            );
        }
    }

    internal void Return(Owner instance)
    {
        var memory = instance._memory;

        if (memory.Length == 0 || !_values.TryRemove(instance, out _))
        {
            throw new InvalidOperationException(
                $"The returned memory was not rented from the pool (length: {memory.Length})."
            );
        }

        Interlocked.Increment(ref _returnedCount);

        if (!TryRelease(memory))
        {
            throw new InvalidOperationException(
                $"Memory<{typeof(T)}>[{memory.Length}] was not from {GetType().FullName}"
            );
        }
    }
}
