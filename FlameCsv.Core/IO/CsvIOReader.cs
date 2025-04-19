using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.IO;

internal abstract class CsvBufferReader<T> : ICsvBufferReader<T> where T : unmanaged
{
    public abstract bool TryReset();
    protected abstract int ReadCore(int minimumRead, Memory<T> buffer);

    protected abstract ValueTask<int> ReadAsyncCore(
        int minimumRead,
        Memory<T> buffer,
        CancellationToken cancellationToken);

    protected abstract void DisposeCore();

    protected readonly MemoryPool<T> _pool;
    private IMemoryOwner<T> _owner;

    private Memory<T> _buffer;
    private ReadOnlyMemory<T> _unread;
    private int _startOffset;

    private Memory<T> AvailableBuffer => _buffer.Slice(_startOffset);

    private bool _completed;
    private readonly int _minimumReadSize;

    protected CsvBufferReader(MemoryPool<T> pool, in CsvReaderOptions options)
    {
        _pool = pool;
        _minimumReadSize = options.MinimumReadSize;
        _owner = pool.Rent(options.BufferSize);
        _buffer = _owner.Memory;
        _unread = ReadOnlyMemory<T>.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvReadResult<T> Read()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_completed) return new CsvReadResult<T>(_unread, true);

        MoveUnreadToFront();

        int read = ReadCore(_minimumReadSize, AvailableBuffer);

        _completed = read < _minimumReadSize;
        _unread = _buffer.Slice(0, _startOffset + read);
        return new CsvReadResult<T>(_unread, _completed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled<CsvReadResult<T>>(cancellationToken);
        if (_completed) return new ValueTask<CsvReadResult<T>>(new CsvReadResult<T>(_unread, true));

        MoveUnreadToFront();

        ValueTask<int> readTask = ReadAsyncCore(_minimumReadSize, AvailableBuffer, cancellationToken);

        if (readTask.IsCompletedSuccessfully)
        {
            int read = readTask.GetAwaiter().GetResult();
            _completed = read < _minimumReadSize;
            _unread = _buffer.Slice(0, _startOffset + read);
            return new ValueTask<CsvReadResult<T>>(new CsvReadResult<T>(_unread, _completed));
        }

        return ReadAsyncAwaited(readTask);
    }

    private async ValueTask<CsvReadResult<T>> ReadAsyncAwaited(ValueTask<int> readTask)
    {
        int read = await readTask.ConfigureAwait(false);
        _completed = read < _minimumReadSize;
        _unread = _buffer.Slice(0, _startOffset + read);
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

        _unread.CopyTo(_buffer);
        _startOffset = _unread.Length;
        _unread = ReadOnlyMemory<T>.Empty;

        // if the fragmented leftover record is too large, grow the buffer
        // TODO PERF: prevent double copy
        if ((_buffer.Length - _startOffset) < _minimumReadSize)
        {
            GrowBuffer();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowBuffer()
    {
        IMemoryOwner<T> newOwner = _pool.Rent(_buffer.Length * 2);
        _unread.CopyTo(newOwner.Memory);
        _owner.Dispose();
        _owner = newOwner;
        _buffer = newOwner.Memory;
        _unread = _buffer.Slice(0, _startOffset);
    }

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

    protected internal bool IsDisposed { get; private set; }

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

internal sealed class StreamBufferReader : CsvBufferReader<byte>
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public StreamBufferReader(
        Stream stream,
        MemoryPool<byte> pool,
        in CsvReaderOptions options) : base(pool, in options)
    {
        _stream = stream;
        _leaveOpen = options.LeaveOpen;
    }

    public override bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_stream.CanSeek)
        {
            _stream.Seek(0, SeekOrigin.Begin);
            return true;
        }

        return ReferenceEquals(_stream, Stream.Null);
    }

    protected override int ReadCore(int minimumRead, Memory<byte> buffer)
    {
        return _stream.ReadAtLeast(buffer.Span, minimumRead, throwOnEndOfStream: false);
    }

    protected override ValueTask<int> ReadAsyncCore(
        int minimumRead,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        return _stream.ReadAtLeastAsync(buffer, minimumRead, throwOnEndOfStream: false, cancellationToken);
    }

    protected override void DisposeCore()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    protected override ValueTask DisposeAsyncCore()
    {
        return _leaveOpen ? default : _stream.DisposeAsync();
    }
}

internal sealed class TextBufferReader : CsvBufferReader<char>
{
    private readonly TextReader _reader;
    private readonly bool _leaveOpen;

    public TextBufferReader(
        TextReader reader,
        MemoryPool<char> pool,
        in CsvReaderOptions options) : base(pool, in options)
    {
        _reader = reader;
        _leaveOpen = options.LeaveOpen;
    }

    public override bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return ReferenceEquals(_reader, TextReader.Null);
    }

    protected override int ReadCore(int minimumRead, Memory<char> buffer)
    {
        int totalRead = 0;
        Span<char> bufferSpan = buffer.Span;

        while (totalRead < minimumRead)
        {
            int read = _reader.Read(bufferSpan.Slice(totalRead));

            if (read == 0)
            {
                return totalRead;
            }

            totalRead += read;
        }

        return totalRead;
    }

    protected override ValueTask<int> ReadAsyncCore(
        int minimumRead,
        Memory<char> buffer,
        CancellationToken cancellationToken)
    {
        ValueTask<int> initialRead = _reader.ReadAsync(buffer, cancellationToken);

        if (initialRead.IsCompletedSuccessfully)
        {
            int read = initialRead.GetAwaiter().GetResult();

            if (read == 0 || read >= minimumRead)
            {
                return new ValueTask<int>(read);
            }

            // we read something, but not enough
            // defer to the async implementation
            return ReadAsyncCoreAwaited(
                _reader.ReadAsync(buffer, cancellationToken),
                minimumRead - read,
                buffer.Slice(read),
                cancellationToken);
        }

        return ReadAsyncCoreAwaited(initialRead, minimumRead, buffer, cancellationToken);
    }

    private async ValueTask<int> ReadAsyncCoreAwaited(
        ValueTask<int> initialRead,
        int minimumRead,
        Memory<char> buffer,
        CancellationToken cancellationToken)
    {
        int totalRead = await initialRead.ConfigureAwait(false);

        if (totalRead == 0)
        {
            return 0;
        }

        while (totalRead < minimumRead)
        {
            int read = await _reader.ReadAsync(buffer.Slice(totalRead), cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return totalRead;
            }

            totalRead += read;
        }

        return totalRead;
    }

    protected override void DisposeCore()
    {
        if (!_leaveOpen)
        {
            _reader.Dispose();
        }
    }
}
