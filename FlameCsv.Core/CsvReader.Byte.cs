using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public static partial class CsvReader
{
    /// <summary>
    /// Asynchronously reads <typeparamref name="TValue"/> from the stream.
    /// </summary>
    /// <remarks>
    /// The stream is closed at the end of the enumeration (on explicit dispose or at the end of a foreach-loop).
    /// To leave it open, use <see cref="PipeReader.Create(Stream,System.IO.Pipelines.StreamPipeReaderOptions?)"/>
    /// and the overload accepting a <see cref="PipeReader"/>.
    /// </remarks>
    /// <param name="stream">Stream to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <param name="leaveOpen">Whether to leave the stream open after it has been read</param>
    /// <returns><see cref="IAsyncEnumerable{T}"/> that reads the CSV one record at a time from the stream.</returns>
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        Stream stream,
        CsvReaderOptions<byte> options,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        var reader = CreatePipeReader(stream, options, leaveOpen);
        return new AsyncCsvRecordEnumerable<byte, TValue, PipeReaderWrapper>(options, new PipeReaderWrapper(reader));
    }

    /// <summary>
    /// Asynchronously reads <typeparamref name="TValue"/> from the reader.
    /// </summary>
    /// <remarks>
    /// The reader is completed at the end of the enumeration (on explicit dispose or at the end of a foreach-loop).
    /// </remarks>
    /// <param name="reader">Pipe reader to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <returns><see cref="IAsyncEnumerable{T}"/> that reads the CSV one record at a time from the reader.</returns>
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        PipeReader reader,
        CsvReaderOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);

        return new AsyncCsvRecordEnumerable<byte, TValue, PipeReaderWrapper>(options, new PipeReaderWrapper(reader));
    }

    /// <summary>
    /// Creates a PipeReader from a Stream.
    /// </summary>
    [StackTraceHidden]
    private static PipeReader CreatePipeReader(
        Stream stream,
        CsvReaderOptions<byte> options,
        bool leaveOpen)
    {
        Guard.CanRead(stream);

        MemoryPool<byte>? memoryPool = null;

        if (options.ArrayPool != ArrayPool<byte>.Shared)
        {
            memoryPool = options.ArrayPool.AllocatingIfNull().AsMemoryPool();
        }

        return PipeReader.Create(
            stream,
            memoryPool is null && !leaveOpen
                ? null
                : new StreamPipeReaderOptions(pool: memoryPool, leaveOpen: leaveOpen));
    }
}
