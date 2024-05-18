using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Reading;

// based HEAVILY on the .NET runtime StreamPipeReader code

/// <summary>
/// Wrapper around a TextReader to facilitate reading it like a PipeReader.
/// </summary>
[DebuggerDisplay(@"\{ TextPipeReader, Buffered: {_bufferedBytes}, Completed: {_readerCompleted} \}")]
internal sealed class TextPipeReader : ICsvPipeReader<char>
{
    private readonly TextReader _innerReader;
    private readonly int _bufferSize;
    private readonly ArrayPool<char> _arrayPool;

    private TextSegment? _readHead;
    private int _readIndex;

    private TextSegment? _readTail;
    private long _bufferedBytes;
    private bool _examinedEverything;

    private bool _readerCompleted;
    private bool _disposed;

    // Mutable struct! Don't make this readonly
    private TextSegmentPool _segmentPool;

    public TextPipeReader(TextReader innerReader, int bufferSize, ArrayPool<char> arrayPool)
    {
        ArgumentNullException.ThrowIfNull(innerReader);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        ArgumentNullException.ThrowIfNull(arrayPool);

        _innerReader = innerReader;
        _bufferSize = bufferSize == -1 ? CsvReader.DefaultBufferSize : bufferSize;
        _arrayPool = arrayPool;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<CsvReadResult<char>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return ThrowObjectDisposedException();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<CsvReadResult<char>>(cancellationToken);
        }

        if (_bufferedBytes > 0 && !_examinedEverything)
        {
            return new(new CsvReadResult<char>(GetCurrentReadOnlySequence(), false));
        }

        if (_readerCompleted)
        {
            return new(new CsvReadResult<char>(default, true));
        }

        return ReadAsyncCore(cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySequence<char> GetCurrentReadOnlySequence()
    {
        // If _readHead is null then _readTail is also null
        return _readHead is null
            ? default
            : new ReadOnlySequence<char>(_readHead, _readIndex, _readTail!, _readTail!.End);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<CsvReadResult<char>> ReadAsyncCore(CancellationToken cancellationToken)
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

        return new CsvReadResult<char>(GetCurrentReadOnlySequence(), _readerCompleted);
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
        TextSegment nextSegment = CreateSegmentUnsynchronized();
        nextSegment.SetOwnedMemory(_bufferSize);
        return nextSegment;
    }

    public void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    {
        if (_disposed)
        {
            ThrowHelper.ThrowObjectDisposedException(nameof(TextPipeReader));
        }

        TextSegment? consumedSegment = (TextSegment?)consumed.GetObject();
        int consumedIndex = consumed.GetInteger();
        TextSegment? examinedSegment = (TextSegment?)examined.GetObject();
        int examinedIndex = examined.GetInteger();

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
            ReturnSegmentUnsynchronized(returnStart);
            returnStart = next;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            TextSegment? segment = _readHead;
            while (segment != null)
            {
                TextSegment returnSegment = segment;
                segment = segment.NextSegment;
                returnSegment.ResetMemory();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    private TextSegment CreateSegmentUnsynchronized()
    {
        if (_segmentPool.TryPop(out TextSegment? segment))
        {
            return segment;
        }

        return new TextSegment(_arrayPool);
    }

    private void ReturnSegmentUnsynchronized(TextSegment segment)
    {
        Debug.Assert(segment != _readHead, "Returning _readHead segment that's in use!");
        Debug.Assert(segment != _readTail, "Returning _readTail segment that's in use!");
        _segmentPool.Push(segment);
    }

    private static ValueTask<CsvReadResult<char>> ThrowObjectDisposedException()
    {
        return ValueTask.FromException<CsvReadResult<char>>(new ObjectDisposedException(nameof(TextPipeReader)));
    }
}
