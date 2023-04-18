using System.Buffers;
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
    /// <param name="cancellationToken">Token to cancel the enumeration</param>
    /// <returns>
    /// <see cref="IAsyncEnumerable{T}"/> that reads records asynchronously line-by-line from the stream
    /// as it is enumerated.
    /// </returns>
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        Stream stream,
        CsvReaderOptions<byte> options,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        return ReadAsync<TValue>(CreatePipeReader(stream, options, leaveOpen), options, cancellationToken);
    }

    /// <summary>
    /// Asynchronously reads <typeparamref name="TValue"/> from the reader.
    /// </summary>
    /// <remarks>
    /// The reader is completed at the end of the enumeration (on explicit dispose or at the end of a foreach-loop).
    /// </remarks>
    /// <param name="reader">Pipe reader to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <param name="cancellationToken">Token to cancel the enumeration</param>
    /// <returns>
    /// <see cref="IAsyncEnumerable{T}"/> that reads records asynchronously line-by-line from the reader
    /// as it is enumerated.
    /// </returns>
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        PipeReader reader,
        CsvReaderOptions<byte> options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);

        if (options.HasHeader)
        {
            return ReadCoreAsync<byte, TValue, PipeReaderWrapper, CsvHeaderProcessor<byte, TValue>>(
                new PipeReaderWrapper(reader),
                new CsvHeaderProcessor<byte, TValue>(options),
                cancellationToken);
        }
        else
        {
            return ReadCoreAsync<byte, TValue, PipeReaderWrapper, CsvProcessor<byte, TValue>>(
                new PipeReaderWrapper(reader),
                new CsvProcessor<byte, TValue>(options),
                cancellationToken);
        }
    }
}
