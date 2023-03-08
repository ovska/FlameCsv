using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Readers.Internal;

/// <summary>
/// Utility class holding on to a rented buffer. The buffer is meant to be reused
/// across multiple writes/reads.
/// </summary>
[DebuggerDisplay(@"\{ BufferOwner: Length: {_rented?.Length}, Disposed: {_disposed} \}")]
internal sealed class BufferOwner<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    internal T[]? _array;
    private bool _disposed;
    private readonly ArrayPool<T> _arrayPool;

    public BufferOwner(CsvReaderOptions<T> options) : this(options.ArrayPool)
    {
    }

    /// <summary>
    /// Initializes a buffer owner using the specified array pool.
    /// </summary>
    public BufferOwner(ArrayPool<T>? arrayPool)
    {
        _arrayPool = arrayPool ?? AllocatingArrayPool<T>.Instance;
    }

    /// <summary>
    /// Returns a buffer of the requested length.
    /// </summary>
    /// <exception cref="ObjectDisposedException" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int length)
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(nameof(BufferOwner<T>));

        _arrayPool.EnsureCapacity(ref _array, length);
        return _array.AsMemory(0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _arrayPool.EnsureReturned(ref _array);

#if DEBUG
        GC.SuppressFinalize(this);
    }

    ~BufferOwner()
    {
        if (!_disposed)
            throw new InvalidOperationException("A BufferOwner was not disposed!");
#endif
    }
}
