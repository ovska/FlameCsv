using System.Buffers;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO.Internal;

internal abstract class CsvBufferReader<T> : ICsvBufferReader<T>
    where T : unmanaged
{
    /// <summary>
    /// Attempts to reset the inner data source to the beginning.
    /// </summary>
    /// <returns><c>true</c> if the data source was reset successfully; otherwise, <c>false</c>.</returns>
    public abstract bool TryReset();

    /// <summary>
    /// Reads data from the inner data source into the provided buffer.
    /// </summary>
    protected abstract int ReadCore(Span<T> buffer);

    public long Position { get; protected set; }

    /// <inheritdoc cref="ReadCore"/>
    protected abstract ValueTask<int> ReadAsyncCore(Memory<T> buffer, CancellationToken cancellationToken);

    private readonly MemoryPool<T> _pool;
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

    protected CsvBufferReader(MemoryPool<T> pool, in CsvIOOptions options)
    {
        _pool = pool;
        _owner = pool.Rent(options.BufferSize);
        _buffer = _owner.Memory;
        _unread = ReadOnlyMemory<T>.Empty;
        _minimumReadSize = options.MinimumReadSize;
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
        return new CsvReadResult<T>(_unread, _completed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<CsvReadResult<T>>(cancellationToken);
        if (_completed)
            return new ValueTask<CsvReadResult<T>>(new CsvReadResult<T>(_unread, true));

        MoveUnreadToFront();

        ValueTask<int> readTask = ReadAsyncCore(AvailableBuffer, cancellationToken);

        if (readTask.IsCompletedSuccessfully)
        {
            int read = readTask.GetAwaiter().GetResult();
            _completed = read == 0;
            _unread = _buffer.Slice(0, _startOffset + read);
            Position += read;
            return new ValueTask<CsvReadResult<T>>(new CsvReadResult<T>(_unread, _completed));
        }

        return ReadAsyncAwaited(readTask);
    }

    private async ValueTask<CsvReadResult<T>> ReadAsyncAwaited(ValueTask<int> readTask)
    {
        int read = await readTask.ConfigureAwait(false);
        _completed = read == 0;
        _unread = _buffer.Slice(0, _startOffset + read);
        Position += read;
        return new CsvReadResult<T>(_unread, _completed);
    }

    public void Advance(int count)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _unread.Length);
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
        IMemoryOwner<T> newOwner = _pool.Rent(_buffer.Length * 2);
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
            DisposeOwner();
            DisposeCore();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            DisposeOwner();
            return DisposeAsyncCore();
        }

        return default;
    }

    private void DisposeOwner()
    {
        try
        {
            _owner.Dispose();
        }
        finally
        {
            _owner = HeapMemoryOwner<T>.Empty;
        }
    }
}
