using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;

namespace FlameCsv.Readers.Internal;

internal sealed class BufferOwner<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    private T[]? _rented;
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
    /// Initializes a buffer owner usin the specified array pool.
    /// </summary>
    public BufferOwner(
        SecurityLevel security,
        ArrayPool<T> arrayPool)
    {
        _arrayPool = arrayPool;
        _rented = System.Array.Empty<T>();
        _clearBuffers = security.ClearBuffers();
    }

    public T[] Array
    {
        get
        {
            if (_rented is null)
                ThrowObjectDisposedException();
            return _rented;
        }
    }

    /// <summary>
    /// Returns a buffer that is at least of the specified size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> GetSpan(int minimumSize)
    {
        EnsureCapacity(minimumSize);
        return _rented.AsSpan();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int minimumSize)
    {
        if (_rented is null)
            ThrowObjectDisposedException();

        if (_rented.Length < minimumSize)
        {
            if (_rented.Length != 0)
            {
                _arrayPool.Return(_rented, clearArray: _clearBuffers);
            }

            _rented = _arrayPool.Rent(minimumSize);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        var rented = _rented;

        if (rented is not null)
        {
            _rented = null;
            _arrayPool.Return(rented, clearArray: _clearBuffers);
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException(typeof(BufferOwner<T>).ToTypeString());
    }
}
