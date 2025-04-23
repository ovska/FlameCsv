using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.IO;

[DebuggerDisplay("[CsvCharBufferWriter] Written: {_unflushed} / {_buffer.Length})")]
internal sealed class CsvCharPipeWriter : ICsvPipeWriter<char>
{
    private static readonly int _defaultBufferSize = 4 * Environment.SystemPageSize / sizeof(char);

    private readonly TextWriter _writer;
    private readonly MemoryPool<char> _allocator;
    private readonly int _bufferSize;
    private readonly int _flushThreshold;
    private readonly bool _leaveOpen;
    private int _unflushed;
    private Memory<char> _buffer;
    private IMemoryOwner<char> _memoryOwner;

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

    public CsvCharPipeWriter(TextWriter writer, MemoryPool<char> allocator, int bufferSize, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (bufferSize == -1)
            bufferSize = _defaultBufferSize;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        _writer = writer;
        _allocator = allocator;
        _leaveOpen = leaveOpen;
        _bufferSize = Math.Max(128, bufferSize);
        _flushThreshold = (int)(_bufferSize * 0.875);
        _memoryOwner = allocator.Rent(_bufferSize);
        _buffer = _memoryOwner.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> GetSpan(int sizeHint = 0)
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
    public Memory<char> GetMemory(int sizeHint = 0)
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
            await _writer.WriteAsync(_buffer.Slice(0, _unflushed), cancellationToken).ConfigureAwait(false);
            _unflushed = 0;
        }
    }

    public void Flush()
    {
        if (HasUnflushedData)
        {
            _writer.Write(_buffer.Slice(0, _unflushed).Span);
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
            try
            {
                if (exception is null)
                {
                    await FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                exception = CsvWriteException.OnComplete(e);
            }
            finally
            {
                _unflushed = -1;
                _memoryOwner = HeapMemoryOwner<char>.Empty;
                _buffer = default;
                if (!_leaveOpen) await _writer.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    public void Complete(Exception? exception)
    {
        using (_memoryOwner)
        {
            try
            {
                if (exception is null)
                {
                    Flush();
                }
            }
            catch (Exception e)
            {
                exception = CsvWriteException.OnComplete(e);
            }
            finally
            {
                _unflushed = -1;
                _memoryOwner = HeapMemoryOwner<char>.Empty;
                _buffer = default;
                if (!_leaveOpen) _writer.Dispose();
            }
        }

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
