using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.IO;

/// <summary>
/// Abstract base class for CSV pipe readers.
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerDisplay(@"\{ CsvPipeReader, Buffered: {_bufferedBytes}, Completed: {_innerCompleted} \}")]
public abstract class CsvPipeReader<T> : ICsvPipeReader<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly MemoryPool<T>? _memoryPool;

    private CsvBufferSegment<T>? _readHead;
    private int _readIndex;
    private CsvBufferSegment<T>? _readTail;
    private long _bufferedBytes;
    private bool _examinedEverything;

    private bool _innerCompleted;

    private readonly int _bufferSize;
    private readonly int _minimumReadSize;

    // Mutable struct! Don't make this readonly
    private BufferSegmentStack<T> _segmentPool;

    /// <summary>
    /// Whether the reader has been disposed.
    /// </summary>
    protected bool IsDisposed { get; private set; }

    /// <summary>
    /// Whether to leave the underlying data source open after completion.
    /// </summary>
    protected bool LeaveOpen { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CsvPipeReader{T}"/>.
    /// </summary>
    /// <param name="memoryPool">Pool to use for the segments</param>
    /// <param name="options"></param>
    protected CsvPipeReader(MemoryPool<T> memoryPool, in CsvReaderOptions options)
    {
        options.EnsureValid(memoryPool);

        _memoryPool = memoryPool == MemoryPool<T>.Shared ? null : memoryPool;
        _segmentPool = new BufferSegmentStack<T>(4);
        _bufferSize = options.BufferSize;
        _minimumReadSize = options.MinimumReadSize;
        LeaveOpen = options.LeaveOpen;
    }

    /// <summary>
    /// Reads data from the inner source into the buffer.
    /// </summary>
    /// <param name="buffer">Buffer to read data into</param>
    /// <returns>How much data was read; or zero if no more data can be read</returns>
    protected abstract int ReadFromInner(Span<T> buffer);

    /// <inheritdoc cref="ReadFromInner" />
    protected abstract ValueTask<int> ReadFromInnerAsync(Memory<T> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Disposes the inner data source according to <see cref="LeaveOpen"/>
    /// </summary>
    /// <remarks>
    /// <see cref="DisposeCore"/> and <see cref="DisposeAsyncCore"/> should provide similar implementations;
    /// only one will be called.
    /// </remarks>
    protected abstract void DisposeCore();

    /// <inheritdoc cref="DisposeCore" />
    protected abstract ValueTask DisposeAsyncCore();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvReadResult<T> Read()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_bufferedBytes > 0 && !_examinedEverything)
        {
            return new CsvReadResult<T>(GetCurrentReadOnlySequence(), false);
        }

        if (_innerCompleted)
        {
            return new CsvReadResult<T>(default, true);
        }

        return ReadSyncCore();
    }

    /// <inheritdoc/>
    public virtual bool TryReset() => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {
            return ThrowObjectDisposedException();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<CsvReadResult<T>>(cancellationToken);
        }

        if (_bufferedBytes > 0 && !_examinedEverything)
        {
            return new(new CsvReadResult<T>(GetCurrentReadOnlySequence(), false));
        }

        if (_innerCompleted)
        {
            return new(new CsvReadResult<T>(default, true));
        }

        return ReadAsyncCore(cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySequence<T> GetCurrentReadOnlySequence()
    {
        // If _readHead is null then _readTail is also null
        return _readHead is null
            ? default
            : new ReadOnlySequence<T>(_readHead, _readIndex, _readTail!, _readTail!.End);
    }

    private CsvReadResult<T> ReadSyncCore()
    {
        EnsureReadTail();

        Memory<T> buffer = _readTail!.AvailableMemory.Slice(_readTail.End);

        int length = ReadFromInner(buffer.Span);

        Debug.Assert(length + _readTail.End <= _readTail.AvailableMemory.Length);

        _readTail.End += length;
        _bufferedBytes += length;

        if (length == 0)
        {
            _innerCompleted = true;
        }

        return new CsvReadResult<T>(GetCurrentReadOnlySequence(), _innerCompleted);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<CsvReadResult<T>> ReadAsyncCore(CancellationToken cancellationToken)
    {
        EnsureReadTail();

        Memory<T> buffer = _readTail!.AvailableMemory.Slice(_readTail.End);

        int length = await ReadFromInnerAsync(buffer, cancellationToken).ConfigureAwait(false);

        Debug.Assert(length + _readTail.End <= _readTail.AvailableMemory.Length);

        _readTail.End += length;
        _bufferedBytes += length;

        if (length == 0)
        {
            _innerCompleted = true;
        }

        return new CsvReadResult<T>(GetCurrentReadOnlySequence(), _innerCompleted);
    }

    private void EnsureReadTail()
    {
        if (_readHead is null)
        {
            Debug.Assert(_readTail is null);
            _readHead = AllocateSegment();
            _readTail = _readHead;
        }
        else
        {
            Debug.Assert(_readTail is not null);
            if (_readTail.WritableBytes < _minimumReadSize)
            {
                CsvBufferSegment<T> nextSegment = AllocateSegment();
                _readTail.SetNext(nextSegment);
                _readTail = nextSegment;
            }
        }
    }

    private CsvBufferSegment<T> AllocateSegment()
    {
        CsvBufferSegment<T> nextSegment = CreateSegmentUnsynchronized();

        if (_memoryPool is null)
        {
            nextSegment.SetOwnedMemory(ArrayPool<T>.Shared.Rent(_bufferSize));
        }
        else
        {
            nextSegment.SetOwnedMemory(_memoryPool.Rent(_bufferSize));
        }

        return nextSegment;
    }

    /// <inheritdoc/>
    public void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        CsvBufferSegment<T>? consumedSegment = (CsvBufferSegment<T>?)consumed.GetObject();
        int consumedIndex = consumed.GetInteger();
        CsvBufferSegment<T>? examinedSegment = (CsvBufferSegment<T>?)examined.GetObject();
        int examinedIndex = examined.GetInteger();

        if (consumedSegment is null || examinedSegment is null)
        {
            return;
        }

        if (_readHead is null)
        {
            Throw.InvalidOperation("Invalid AdvanceTo, head is null");
        }

        CsvBufferSegment<T> returnStart = _readHead;
        CsvBufferSegment<T>? returnEnd = consumedSegment;

        long consumedBytes = CsvBufferSegment<T>.GetLength(returnStart, _readIndex, consumedSegment, consumedIndex);

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
            CsvBufferSegment<T>? nextBlock = returnEnd.NextSegment;
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
            CsvBufferSegment<T> next = returnStart.NextSegment!;
            ReturnSegmentUnsynchronized(returnStart);
            returnStart = next;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        DisposeSegments();
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        DisposeSegments();
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private void DisposeSegments()
    {
        CsvBufferSegment<T>? segment = _readHead;
        while (segment is not null)
        {
            CsvBufferSegment<T> returnSegment = segment;
            segment = segment.NextSegment;
            returnSegment.Reset();
        }
    }

    private CsvBufferSegment<T> CreateSegmentUnsynchronized()
    {
        if (_segmentPool.TryPop(out CsvBufferSegment<T>? segment))
        {
            return segment;
        }

        return new CsvBufferSegment<T>();
    }

    private void ReturnSegmentUnsynchronized(CsvBufferSegment<T> segment)
    {
        Debug.Assert(segment != _readHead, "Returning _readHead segment that's in use!");
        Debug.Assert(segment != _readTail, "Returning _readTail segment that's in use!");
        segment.Reset();
        _segmentPool.Push(segment);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private ValueTask<CsvReadResult<T>> ThrowObjectDisposedException()
    {
        return ValueTask.FromException<CsvReadResult<T>>(new ObjectDisposedException(GetType().Name));
    }
}
