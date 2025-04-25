using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.IO;

[DebuggerDisplay("[CsvCharBufferWriter] Written: {_unflushed} / {_buffer.Length})")]
internal sealed class TextBufferWriter : ICsvBufferWriter<char>
{
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

    public bool NeedsFlush
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _unflushed >= _flushThreshold;
    }

    public TextBufferWriter(TextWriter writer, MemoryPool<char> allocator, in CsvIOOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _allocator = allocator;
        _leaveOpen = options.LeaveOpen;
        _bufferSize = options.BufferSize;
        _flushThreshold = Math.Max(128, (int)(_bufferSize * (31.0 / 32.0)));
        _memoryOwner = allocator.Rent(_bufferSize);
        _buffer = _memoryOwner.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<char> GetMemory(int sizeHint = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        int remaining = Remaining;

        if (remaining < sizeHint || remaining == 0)
        {
            ResizeBuffer(sizeHint);
        }

        Debug.Assert(Remaining >= sizeHint);
        return _buffer.Slice(_unflushed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResizeBuffer(int sizeHint)
    {
        _allocator.EnsureCapacity(
            ref _memoryOwner,
            minimumLength: _unflushed + Math.Max(sizeHint, _bufferSize),
            copyOnResize: true);
        _buffer = _memoryOwner.Memory;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int length)
    {
        if ((uint)length > (uint)Remaining)
            Throw.Argument_OutOfRange(nameof(length));

        _unflushed += length;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_unflushed == -1) return default;

        var memory = _buffer.Slice(0, _unflushed);
        _unflushed = 0;

        return memory.IsEmpty ? default : new(_writer.WriteAsync(memory, cancellationToken));
    }

    public void Flush()
    {
        if (_unflushed == -1) return;

        var memory = _buffer.Slice(0, _unflushed);
        _unflushed = 0;

        if (!memory.IsEmpty)
        {
            _writer.Write(memory.Span);
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
