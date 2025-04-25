using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Unicode;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.IO;

internal sealed class Utf8StreamWriter : ICsvBufferWriter<char>
{
    private readonly Stream _stream;
    private readonly MemoryPool<char> _allocator;
    private readonly int _bufferSize;
    private readonly int _flushThreshold;
    private readonly bool _leaveOpen;
    private int _unflushed;
    private Memory<char> _buffer;
    private IMemoryOwner<char> _memoryOwner;

    private byte[] _byteBuffer;

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

    public Utf8StreamWriter(Stream stream, MemoryPool<char> allocator, in CsvIOOptions options)
    {
        Guard.CanWrite(stream);
        _stream = stream;
        _allocator = allocator;
        _leaveOpen = options.LeaveOpen;
        _bufferSize = options.BufferSize;
        _flushThreshold = Math.Max(128, (int)(_bufferSize * (31.0 / 32.0)));
        _memoryOwner = allocator.Rent(_bufferSize);
        _buffer = _memoryOwner.Memory;

        // assume most data is ASCII, with a few multi-byte characters here and there
        _byteBuffer = ArrayPool<byte>.Shared.Rent(_bufferSize * 2);
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

        var memory = Transcode(out bool moreData);

        if (memory.IsEmpty)
        {
            return default;
        }

        // if this is the only chunk, write it directly
        if (!moreData)
        {
            Debug.Assert(_unflushed == 0, $"Unflushed bytes should be 0: {_unflushed}");
            return _stream.WriteAsync(memory, cancellationToken);
        }

        return FlushAsyncCore(memory, cancellationToken);
    }

    private async ValueTask FlushAsyncCore(
        ReadOnlyMemory<byte> memory,
        CancellationToken cancellationToken = default)
    {
        await _stream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);

        bool moreData;

        do
        {
            memory = Transcode(out moreData);

            if (memory.IsEmpty)
            {
                break;
            }

            await _stream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);
        } while (moreData);

        Debug.Assert(_unflushed == 0, $"Unflushed bytes should be 0: {_unflushed}");
    }

    public void Flush()
    {
        if (_unflushed == -1) return;

        bool moreData;

        do
        {
            var memory = Transcode(out moreData);

            if (memory.IsEmpty)
            {
                break;
            }

            _stream.Write(memory.Span);
        } while (moreData);

        Debug.Assert(_unflushed == 0, $"Unflushed bytes should be 0: {_unflushed}");
    }

    private ReadOnlyMemory<byte> Transcode(out bool moreData)
    {
        moreData = false;
        ReadOnlySpan<char> chars = _buffer.Span.Slice(0, _unflushed);

        if (chars.IsEmpty)
        {
            _unflushed = 0;
            return ReadOnlyMemory<byte>.Empty;
        }

        int totalRead = 0;
        int totalWritten = 0;

        while (!chars.IsEmpty)
        {
            OperationStatus status = Utf8.FromUtf16(
                chars,
                _byteBuffer.AsSpan(totalWritten),
                out int charsRead,
                out int bytesWritten,
                replaceInvalidSequences: true,
                isFinalBlock: true); // values should be self-contained

            totalWritten += bytesWritten;
            totalRead += charsRead;
            chars = chars.Slice(charsRead);

            if (status == OperationStatus.Done)
            {
                break;
            }

            if (status == OperationStatus.DestinationTooSmall)
            {
                moreData = true;
                break;
            }
        }

        _unflushed -= totalRead;
        return _byteBuffer.AsMemory(0, totalWritten);
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
                ArrayPool<byte>.Shared.Return(_byteBuffer);
                _byteBuffer = [];
                if (!_leaveOpen) await _stream.DisposeAsync().ConfigureAwait(false);
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
                ArrayPool<byte>.Shared.Return(_byteBuffer);
                _byteBuffer = [];
                if (!_leaveOpen) _stream.Dispose();
            }
        }

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
