using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO.Internal;

internal abstract class CsvBufferReader<T> : ICsvBufferReader<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Attempts to reset the inner data source to the beginning.
    /// </summary>
    /// <returns><c>true</c> if the data source was reset successfully; otherwise, <c>false</c>.</returns>
    public bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (TryResetCore())
        {
            Position = 0;
            return true;
        }

        return false;
    }

    protected abstract bool TryResetCore();

    /// <summary>
    /// Reads data from the inner data source into the provided buffer.
    /// </summary>
    protected abstract int ReadCore(Span<T> buffer);

    public long Position { get; private set; }

    /// <inheritdoc cref="ReadCore"/>
    protected abstract ValueTask<int> ReadAsyncCore(Memory<T> buffer, CancellationToken cancellationToken);

    private readonly IBufferPool _bufferPool;
    private IMemoryOwner<T> _owner;

    /// <summary>
    /// Minimum length of data yielded by Read and ReadAsync before more data is read from the underlying source.
    /// </summary>
    private readonly int _minimumReadSize;

    /// <summary>Buffer to read the data to</summary>
    private Memory<T> _buffer;

    /// <summary>Unprocessed characters in the buffer</summary>
    private ReadOnlyMemory<T> _unread;

    /// <summary>Number of characters at the start of the buffer that have not been processed</summary>
    private int _startOffset;

    /// <summary>Empty buffer available to read more data into</summary>
    private Memory<T> AvailableBuffer => _buffer.Slice(_startOffset);

    /// <summary>Whether the previous call to Read returned 0 characters</summary>
    private bool _completed;
    private bool _preambleRead;

    protected CsvBufferReader(in CsvIOOptions options)
    {
        _bufferPool = options.EffectiveBufferPool;
        _owner = _bufferPool.Rent<T>(options.BufferSize);
        _buffer = _owner.Memory;
        _unread = ReadOnlyMemory<T>.Empty;
        _minimumReadSize = options.MinimumReadSize;

        if (typeof(T) == typeof(byte))
        {
            _preambleRead = false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvReadResult<T> Read()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_completed)
            return new CsvReadResult<T>(_unread, true);

        MoveUnreadToFront();

        int read = ReadCore(AvailableBuffer.Span);

        _completed = read == 0;
        _unread = _buffer.Slice(0, _startOffset + read);
        Position += read;

        if (typeof(T) == typeof(byte) && !_preambleRead)
        {
            return ReadPreamble();
        }
        else
        {
            return new CsvReadResult<T>(_unread, _completed);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_completed)
            return new CsvReadResult<T>(_unread, true);

        MoveUnreadToFront();

        int read = await ReadAsyncCore(AvailableBuffer, cancellationToken).ConfigureAwait(false);
        _completed = read == 0;
        _unread = _buffer.Slice(0, _startOffset + read);
        Position += read;

        if (typeof(T) == typeof(byte) && !_preambleRead)
        {
            return ReadPreamble();
        }
        else
        {
            return new CsvReadResult<T>(_unread, _completed);
        }
    }

    public void Advance(int count)
    {
        _unread = _unread.Slice(count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MoveUnreadToFront()
    {
        if (_unread.IsEmpty)
        {
            _startOffset = 0;
            return;
        }

        _startOffset = _unread.Length;

        // if the fragmented leftover record is too large, grow the buffer
        // otherwise, just move the unread data to the front
        if ((_buffer.Length - _startOffset) < _minimumReadSize)
        {
            ResizeBuffer();
        }
        else
        {
            _unread.CopyTo(_buffer);
        }

        _unread = ReadOnlyMemory<T>.Empty;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResizeBuffer()
    {
        IMemoryOwner<T> newOwner = _bufferPool.Rent<T>(_buffer.Length * 2);
        _buffer = newOwner.Memory;
        _unread.CopyTo(_buffer);

        _owner.Dispose();
        _owner = newOwner;
        _unread = _buffer.Slice(0, _startOffset);
    }

    /// <summary>
    /// Synchronously disposes the inner data source.
    /// </summary>
    protected abstract void DisposeCore();

    /// <summary>
    /// When overridden, asynchronously disposes the inner data source.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore()
    {
        try
        {
            DisposeCore();
            return default;
        }
        catch (Exception e)
        {
            return ValueTask.FromException(e);
        }
    }

    protected bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            DisposeBuffers();
            DisposeCore();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            DisposeBuffers();
            return DisposeAsyncCore();
        }

        return default;
    }

    private void DisposeBuffers()
    {
        try
        {
            _owner.Dispose();
            _unread = default;
            _buffer = default;
        }
        finally
        {
            _owner = null!;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private CsvReadResult<T> ReadPreamble()
    {
        if (typeof(T) != typeof(byte))
            throw new UnreachableException();

        if (Unsafe.BitCast<ReadOnlySpan<T>, ReadOnlySpan<byte>>(_unread.Span) is [0xEF, 0xBB, 0xBF, ..])
        {
            _unread = _unread.Slice(3);
        }

        _preambleRead = true;
        return new CsvReadResult<T>(_unread, _completed);
    }
}
