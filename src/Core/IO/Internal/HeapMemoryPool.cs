using System.Buffers;
using System.Diagnostics;

namespace FlameCsv.IO.Internal;

internal class HeapMemoryPool<T> : MemoryPool<T>
{
    public static HeapMemoryPool<T> Instance { get; } = new();

    public override int MaxBufferSize => Array.MaxLength;

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        if (minBufferSize == 0)
        {
            return HeapMemoryOwner<T>.Empty;
        }

        if (minBufferSize == -1)
        {
            minBufferSize = 4096;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(minBufferSize);

        T[] array = GC.AllocateUninitializedArray<T>((int)BitOperations.RoundUpToPowerOf2((uint)minBufferSize));
        return new HeapMemoryOwner<T>(array);
    }

    protected override void Dispose(bool disposing)
    {
        // no-op
    }

    // ReSharper disable once UnusedMember.Global
    [Obsolete("Use HeapMemoryPool<T>.Instance instead", true)]
    public static new MemoryPool<T> Shared => throw new UnreachableException();
}

internal class HeapMemoryOwner<T>(T[] array) : IMemoryOwner<T>
{
#if DEBUG
    private bool _disposed;
#endif

    public static HeapMemoryOwner<T> Empty { get; } = new([]);

    public Memory<T> Memory
    {
        get
        {
#if DEBUG
            ObjectDisposedException.ThrowIf(_disposed, this);
#endif

            return array;
        }
    }

    public void Dispose()
    {
#if DEBUG
        // Don't dispose the empty instance
        if (array.Length != 0)
            _disposed = true;
#endif
    }
}
