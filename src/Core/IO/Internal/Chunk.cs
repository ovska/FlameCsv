using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.IO.Internal;

/// <summary>
/// Chunks for parallel CSV reading.
/// </summary>
/// <typeparam name="T"></typeparam>
internal sealed class Chunk<T> : RecordOwner<T>, IDisposable, IEnumerable<CsvRecordRef<T>>, IHasOrder
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Index of the chunk.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Data of the chunk.
    /// </summary>
    public ReadOnlyMemory<T> Data { get; }

    /// <summary>
    /// Records available in the chunk.
    /// </summary>
    public RecordBuffer RecordBuffer => _recordBuffer;

    /// <summary>
    /// Memory owner for the chunk data.
    /// </summary>
    private readonly IDisposable? _owner;

    private readonly IBufferPool _bufferPool;

    private IMemoryOwner<T>? _unescapeBuffer;

    internal byte _disposed;

    public Chunk(
        int order,
        CsvOptions<T> options,
        ReadOnlyMemory<T> data,
        IBufferPool bufferPool,
        IDisposable? owner,
        RecordBuffer recordBuffer
    )
        : base(options, recordBuffer)
    {
        Order = order;
        Data = data;
        _bufferPool = bufferPool;
        _owner = owner;
    }

    public bool TryPop(out CsvRecordRef<T> record)
    {
        if (RecordBuffer.TryPop(out RecordView view))
        {
            record = new(this, ref MemoryMarshal.GetReference(Data.Span), view);
            return true;
        }

        record = default;
        return false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        RecordBuffer.Dispose();
        _owner?.Dispose();
        _unescapeBuffer?.Dispose();
    }

    public IEnumerator<CsvRecordRef<T>> GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    internal override Span<T> GetUnescapeBuffer(int length)
    {
        // allocate a new buffer if the requested length is larger than the stack buffer
        if (_unescapeBuffer is null || _unescapeBuffer.Memory.Length < length)
        {
            _unescapeBuffer?.Dispose();
            _unescapeBuffer = _bufferPool.Rent<T>(length);
        }

        return _unescapeBuffer.Memory.Span;
    }

    [SkipLocalsInit]
    private sealed class Enumerator : IEnumerator<CsvRecordRef<T>>
    {
        private readonly Chunk<T> _chunk;
        private RecordView _current;

        public Enumerator(Chunk<T> chunk)
        {
            _chunk = chunk;
        }

        public CsvRecordRef<T> Current => new(_chunk, ref MemoryMarshal.GetReference(_chunk.Data.Span), _current);

        object IEnumerator.Current => throw new NotSupportedException();

        public bool MoveNext() => _chunk.RecordBuffer.TryPop(out _current);

        public void Reset() => throw new NotSupportedException();

        public void Dispose() => _chunk.Dispose();
    }
}
