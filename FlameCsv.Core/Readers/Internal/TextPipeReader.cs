using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Readers.Internal;

internal readonly struct TextReadResult
{
    /// <summary>
    /// Buffer containing the read data. May be empty.
    /// </summary>
    public ReadOnlySequence<char> Buffer { get; }

    /// <summary>
    /// There is no more data to read.
    /// </summary>
    public bool IsCompleted { get; }

    public TextReadResult(ReadOnlySequence<char> buffer, bool isCompleted)
    {
        Buffer = buffer;
        IsCompleted = isCompleted;
    }
}

// based HEAVILY on the .NET runtime StreamPipeReader code
internal sealed class TextPipeReader : IDisposable
{
    private readonly TextReader _innerReader;
    private readonly int _bufferSize;

    private TextSegment? _readHead;
    private int _readIndex;

    private TextSegment? _readTail;
    private long _bufferedBytes;
    private bool _examinedEverything;

    private bool _readerCompleted;
    private bool _disposed;

    public TextPipeReader(TextReader innerReader, int bufferSize)
    {
        _innerReader = innerReader;
        _bufferSize = bufferSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TextReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<TextReadResult>(cancellationToken);
        }

        if (TryRead(out var buffer))
        {
            return new(new TextReadResult(buffer, false));
        }

        if (_readerCompleted)
        {
            return new(new TextReadResult(default, true));
        }

        return ReadAsyncCore(cancellationToken);
    }

    private bool TryRead(out ReadOnlySequence<char> result)
    {
        ThrowIfDisposed();

        if (_bufferedBytes > 0 && !_examinedEverything)
        {
            result = GetCurrentReadOnlySequence();
            return true;
        }

        result = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySequence<char> GetCurrentReadOnlySequence()
    {
        // If _readHead is null then _readTail is also null
        return _readHead is null
            ? default
            : new ReadOnlySequence<char>(_readHead, _readIndex, _readTail!, _readTail!.End);
    }

    private async ValueTask<TextReadResult> ReadAsyncCore(CancellationToken cancellationToken)
    {
        EnsureReadTail();

        Memory<char> buffer = _readTail!.AvailableMemory.Slice(_readTail.End);

        int length = await _innerReader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

        Debug.Assert(length + _readTail.End <= _readTail.AvailableMemory.Length);

        _readTail.End += length;
        _bufferedBytes += length;

        if (length == 0)
        {
            _readerCompleted = true;
        }

        return new TextReadResult(GetCurrentReadOnlySequence(), _readerCompleted);
    }

    private void EnsureReadTail()
    {
        if (_readHead == null)
        {
            Debug.Assert(_readTail == null);
            _readHead = AllocateSegment();
            _readTail = _readHead;
        }
        else
        {
            Debug.Assert(_readTail != null);
            if (_readTail.WritableBytes < _bufferSize)
            {
                TextSegment nextSegment = AllocateSegment();
                _readTail.SetNext(nextSegment);
                _readTail = nextSegment;
            }
        }
    }

    private TextSegment AllocateSegment()
    {
        TextSegment nextSegment = new();
        nextSegment.SetOwnedMemory(ArrayPool<char>.Shared.Rent(_bufferSize));
        return nextSegment;
    }

    public void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    {
        ThrowIfDisposed();

        AdvanceTo(
            (TextSegment?)consumed.GetObject(),
            consumed.GetInteger(),
            (TextSegment?)examined.GetObject(),
            examined.GetInteger());
    }

    private void AdvanceTo(
        TextSegment? consumedSegment,
        int consumedIndex,
        TextSegment? examinedSegment,
        int examinedIndex)
    {
        if (consumedSegment == null || examinedSegment == null)
        {
            return;
        }

        if (_readHead == null)
        {
            ThrowHelper.ThrowInvalidOperationException("Invalid AdvanceTo, head is null");
        }

        TextSegment returnStart = _readHead;
        TextSegment? returnEnd = consumedSegment;

        long consumedBytes = TextSegment.GetLength(returnStart, _readIndex, consumedSegment, consumedIndex);

        _bufferedBytes -= consumedBytes;

        Debug.Assert(_bufferedBytes >= 0);

        _examinedEverything = false;

        if (examinedSegment == _readTail)
        {
            _examinedEverything = examinedIndex == _readTail.End;
        }

        if (_bufferedBytes == 0)
        {
            returnEnd = null;
            _readHead = null;
            _readTail = null;
            _readIndex = 0;
        }
        else if (consumedIndex == returnEnd.Length)
        {
            TextSegment? nextBlock = returnEnd.NextSegment;
            _readHead = nextBlock;
            _readIndex = 0;
            returnEnd = nextBlock;
        }
        else
        {
            _readHead = consumedSegment;
            _readIndex = consumedIndex;
        }

        while (returnStart != returnEnd)
        {
            TextSegment next = returnStart.NextSegment!;
            returnStart.ResetMemory();
            returnStart = next;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        TextSegment? segment = _readHead;
        while (segment != null)
        {
            TextSegment returnSegment = segment;
            segment = segment.NextSegment;

            returnSegment.ResetMemory();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(nameof(TextPipeReader));
    }
}
