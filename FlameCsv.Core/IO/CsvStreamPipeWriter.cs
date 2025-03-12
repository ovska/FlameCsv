using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.IO;

[DebuggerDisplay("[CsvStreamBufferWriter] Written: {_unflushed} / {_buffer.Length})")]
internal sealed class CsvStreamPipeWriter : ICsvPipeWriter<byte>
{
    private static readonly int _defaultBufferSize = Environment.SystemPageSize;

    private readonly Stream _stream;
    private readonly MemoryPool<byte> _allocator;
    private readonly int _bufferSize;
    private readonly int _flushThreshold;
    private readonly bool _leaveOpen;
    private int _unflushed;
    private Memory<byte> _buffer;
    private IMemoryOwner<byte> _memoryOwner;

    public int Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer.Length - _unflushed;
    }

    public bool HasUnflushedData
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _unflushed > 0;
    }

    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _unflushed >= _flushThreshold;
    }

    public CsvStreamPipeWriter(Stream stream, MemoryPool<byte> allocator, int bufferSize, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (bufferSize == -1)
            bufferSize = _defaultBufferSize;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        _stream = stream;
        _allocator = allocator;
        _leaveOpen = leaveOpen;
        _bufferSize = Math.Max(128, bufferSize);
        _flushThreshold = (int)(_bufferSize * 0.875);
        _memoryOwner = allocator.Rent(_bufferSize);
        _buffer = _memoryOwner.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        if (Remaining < sizeHint || Remaining == 0)
        {
            _allocator.EnsureCapacity(
                ref _memoryOwner,
                minimumLength: _unflushed + Math.Max(sizeHint, _bufferSize),
                copyOnResize: true);
            _buffer = _memoryOwner.Memory;
        }

        Debug.Assert(Remaining >= sizeHint);
        return _buffer.Slice(_unflushed).Span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        if (Remaining < sizeHint || Remaining == 0)
        {
            _allocator.EnsureCapacity(
                ref _memoryOwner,
                minimumLength: _unflushed + Math.Max(sizeHint, _bufferSize),
                copyOnResize: true);
            _buffer = _memoryOwner.Memory;
        }

        Debug.Assert(Remaining >= sizeHint);
        return _buffer.Slice(_unflushed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int length)
    {
        if ((uint)length > (uint)Remaining)
            Throw.Argument_OutOfRange(nameof(length));

        _unflushed += length;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (HasUnflushedData)
        {
            await _stream.WriteAsync(_buffer.Slice(0, _unflushed), cancellationToken).ConfigureAwait(false);
            _unflushed = 0;
        }
    }

    public void Flush()
    {
        if (HasUnflushedData)
        {
            _stream.Write(_buffer.Slice(0, _unflushed).Span);
            _unflushed = 0;
        }
    }

    public async ValueTask CompleteAsync(
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            exception ??= new OperationCanceledException(cancellationToken);

        using (_memoryOwner)
        {
            if (exception is null)
            {
                try
                {
                    await FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    exception = new CsvWriteException(
                        "Exception occured while flushing the writer for the final time.",
                        e);
                }
                finally
                {
                    _unflushed = -1;

                    if (!_leaveOpen)
                        await _stream.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        _memoryOwner = HeapMemoryOwner<byte>.Empty;
        _buffer = default;

        if (exception is not null)
            throw exception;
    }

    public void Complete(Exception? exception)
    {
        using (_memoryOwner)
        {
            if (exception is null && HasUnflushedData)
            {
                try
                {
                    Flush();
                }
                catch (Exception e)
                {
                    exception = new CsvWriteException(
                        "Exception occured while flushing the writer for the final time.",
                        e);
                }
                finally
                {
                    _unflushed = -1;

                    if (!_leaveOpen)
                        _stream.Dispose();
                }
            }
        }

        _memoryOwner = HeapMemoryOwner<byte>.Empty;
        _buffer = default;

        if (exception is not null)
        {
            if (exception is not CsvWriteException)
            {
                throw new CsvWriteException($"Exception occured while writing to {GetType()}.", exception);
            }

            throw exception;
        }
    }
}
