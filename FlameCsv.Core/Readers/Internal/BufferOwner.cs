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
    private T[]? _rented;
    private bool _disposed;
    private readonly ArrayPool<T> _arrayPool;
    private readonly bool _clearBuffers;

    /// <summary>
    /// Initializes a buffer owner using the shared array pool of <typeparamref name="T"/>.
    /// </summary>
    public BufferOwner(SecurityLevel security = SecurityLevel.Strict)
        : this(security, ArrayPool<T>.Shared)
    {
    }

    /// <summary>
    /// Initializes a buffer owner using the specified array pool.
    /// </summary>
    public BufferOwner(
        SecurityLevel security,
        ArrayPool<T> arrayPool)
    {
        _arrayPool = arrayPool;
        _clearBuffers = security.ClearBuffers();
    }

    /// <summary>
    /// Returns a buffer of the requested length.
    /// </summary>
    /// <exception cref="ObjectDisposedException" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int length)
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(typeof(BufferOwner<T>).ToTypeString());

        _arrayPool.EnsureCapacity(ref _rented, capacity: length, clearArray: _clearBuffers);
        return _rented.AsSpan(0, length);
    }
    
    /// <summary>
    /// Returns a buffer of the requested length.
    /// </summary>
    /// <exception cref="ObjectDisposedException" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> GetMemory(int length)
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(typeof(BufferOwner<T>).ToTypeString());

        _arrayPool.EnsureCapacity(ref _rented, capacity: length, clearArray: _clearBuffers);
        return _rented.AsMemory(0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        var rented = _rented;

        if (rented is not null)
        {
            _rented = null;
            _arrayPool.Return(rented, clearArray: _clearBuffers);
        }
    }
}
