using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Readers;

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
    /// <param name="cancellationToken">Token to cancel the enumeration</param>
    /// <returns>
    /// <see cref="IAsyncEnumerable{T}"/> that reads records asynchronously line-by-line from the stream
    /// as it is enumerated.
    /// </returns>
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        Stream stream,
        CsvReaderOptions<byte> options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);
        return ReadAsync<TValue>(PipeReader.Create(stream), options, cancellationToken);
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
    /// <see cref="IAsyncEnumerable{T}"/> that reads records asynchronously line-by-line from the stream
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
            var processor = new CsvHeaderProcessor<byte, TValue>(options);
            return ReadAsyncInternal<TValue, CsvHeaderProcessor<byte, TValue>>(reader, processor, cancellationToken);
        }
        else
        {
            var processor = new CsvProcessor<byte, TValue>(options);
            return ReadAsyncInternal<TValue, CsvProcessor<byte, TValue>>(reader, processor, cancellationToken);
        }
    }

    private static async IAsyncEnumerable<TValue> ReadAsyncInternal<TValue, TProcessor>(
        PipeReader reader,
        TProcessor processor,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TProcessor : struct, ICsvProcessor<byte, TValue>
    {
        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (processor.TryContinueRead(ref buffer, out TValue value))
                {
                    yield return value;
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    // Read leftover data if there was no final newline
                    if (!buffer.IsEmpty && processor.TryReadRemaining(in buffer, out TValue value))
                    {
                        yield return value;
                    }

                    break;
                }
            }
        }
        finally
        {
            processor.Dispose();
            await reader.CompleteAsync();
        }
    }
}
