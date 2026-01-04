using System.Buffers;
using System.Collections;
using System.Runtime.InteropServices;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.IO.Internal;

/// <summary>
/// Chunks for parallel CSV reading.
/// </summary>
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

    public long Position { get; init; }

    public int LineNumber { get; init; }

    /// <summary>
    /// Memory owner for the chunk data.
    /// </summary>
    private readonly IMemoryOwner<T> _owner;
    private IMemoryOwner<T>? _unescapeBuffer;

    private readonly IBufferPool _bufferPool;

    private bool _disposed;

    /// <inheritdoc/>
    public override bool IsDisposed => _disposed;

    public Chunk(
        int order,
        int lineNumber,
        long position,
        CsvOptions<T> options,
        ReadOnlyMemory<T> data,
        IBufferPool bufferPool,
        IMemoryOwner<T> owner,
        RecordBuffer recordBuffer
    )
        : base(options, recordBuffer)
    {
        Order = order;
        Data = data;
        Position = position;
        LineNumber = lineNumber;
        _bufferPool = bufferPool;
        _owner = owner;
        _disposed = false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, true))
        {
            return;
        }

#if DEBUG || FULL_TEST_SUITE
        GC.SuppressFinalize(this);
#endif

        _recordBuffer.Dispose();
        _owner.Dispose();
        _unescapeBuffer?.Dispose();
    }

    public IEnumerator<CsvRecordRef<T>> GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal override Span<T> GetUnescapeBuffer(int length)
    {
        Check.False(IsDisposed, "Chunk already disposed.");
        _bufferPool.EnsureCapacity(ref _unescapeBuffer, length, copyOnResize: false);
        return _unescapeBuffer.Memory.Span.Slice(0, length);
    }

#if DEBUG || FULL_TEST_SUITE
    ~Chunk()
    {
        Check.True(
            _disposed,
            $"Chunk {Order} was not disposed before being finalized (line {LineNumber} position {Position})."
        );
    }
#endif

    private sealed class Enumerator : IEnumerator<CsvRecordRef<T>>
    {
        private readonly Chunk<T> _chunk;
        private RecordView _current;
        private bool _valid;

        public Enumerator(Chunk<T> chunk)
        {
            Check.False(chunk.IsDisposed, "Chunk already disposed.");
            _chunk = chunk;
            _valid = false;
            _current = default;
        }

        public CsvRecordRef<T> Current
        {
            get
            {
                Check.False(_chunk.IsDisposed, "Chunk already disposed.");
                Check.True(_valid, "Enumerator is not valid (not started or already finished).");
                return new(_chunk, ref MemoryMarshal.GetReference(_chunk.Data.Span), _current);
            }
        }

        public bool MoveNext()
        {
            Check.False(_chunk.IsDisposed, "Chunk already disposed.");
            return _valid = _chunk._recordBuffer.TryPop(out _current);
        }

        public void Dispose() => _chunk.Dispose();

        void IEnumerator.Reset() => throw new NotSupportedException();

        object IEnumerator.Current => throw new NotSupportedException();
    }
}
