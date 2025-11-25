using System.Buffers;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO.Internal;

internal sealed class Allocator<T>(IBufferPool bufferPool) : IDisposable
    where T : unmanaged
{
    private IMemoryOwner<T> _memoryOwner = HeapMemoryOwner<T>.Empty;

    public Span<T> GetSpan(int length)
    {
        ObjectDisposedException.ThrowIf(_memoryOwner is null, this);

        Memory<T> memory = _memoryOwner.Memory;

        if (memory.Length < length)
        {
            memory = EnsureCapacity(length);
        }

        return memory.Slice(0, length).Span;
    }

    public void Dispose()
    {
        _memoryOwner = null!;
        _memoryOwner?.Dispose();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Memory<T> EnsureCapacity(int minimumLength)
    {
        ObjectDisposedException.ThrowIf(_memoryOwner is null, this);

        var newOwner = bufferPool.Rent<T>(minimumLength);

        Memory<T> newMemory = newOwner.Memory;

        _memoryOwner.Dispose();
        _memoryOwner = newOwner;
        return newMemory;
    }
}
