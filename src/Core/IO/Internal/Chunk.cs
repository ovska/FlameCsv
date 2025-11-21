using System.Runtime.InteropServices;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

namespace FlameCsv.IO.Internal;

/// <summary>
/// Chunks for parallel CSV reading.
/// </summary>
/// <typeparam name="T"></typeparam>
internal sealed class Chunk<T> : IDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// 0-based start position of the chunk in the data source.
    /// </summary>
    public long Position { get; }

    /// <summary>
    /// Data of the chunk.
    /// </summary>
    public ReadOnlyMemory<T> Data { get; }

    /// <summary>
    /// Records available in the chunk.
    /// </summary>
    public RecordBuffer RecordBuffer { get; }

    /// <summary>
    /// Memory owner for the chunk data.
    /// </summary>
    private readonly IDisposable? _owner;

    /// <summary>
    /// Reader that produced this chunk.
    /// </summary>
    private readonly CsvReaderBase<T> _reader;

    internal byte _disposed;

    public Chunk(
        long position,
        ReadOnlyMemory<T> data,
        IDisposable? owner,
        RecordBuffer recordBuffer,
        CsvReaderBase<T> reader
    )
    {
        Position = position;
        Data = data;
        _owner = owner;
        RecordBuffer = recordBuffer;
        _reader = reader;
    }

    public bool TryPop(out CsvRecordRef<T> record)
    {
        if (RecordBuffer.TryPop(out RecordView view))
        {
            record = new(_reader, RecordBuffer, ref MemoryMarshal.GetReference(Data.Span), view);
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
    }
}
