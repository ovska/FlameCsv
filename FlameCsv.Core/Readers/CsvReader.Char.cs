using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

public static partial class CsvReader
{
    /// <summary>
    /// Asynchronously reads <typeparamref name="TValue"/> from the stream using the specified encoding.
    /// </summary>
    /// <remarks>
    /// The reader is completed at the end of the enumeration (on explicit dispose or at the end of a foreach-loop).
    /// </remarks>
    /// <param name="stream">Stream reader to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <param name="encoding">
    /// Encoding to initialize the <see cref="StreamWriter"/> with, set to null to auto-detect (default behavior)
    /// </param>
    /// <param name="leaveOpen">
    /// If <see langword="true"/>, the stream and writer are not disposed at the end of the enumeration
    /// </param>
    /// <param name="cancellationToken">Token to cancel the enumeration</param>
    /// <returns>
    /// <see cref="IAsyncEnumerable{T}"/> that reads records asynchronously line-by-line from the stream
    /// as it is enumerated.
    /// </returns>
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        Stream stream,
        CsvReaderOptions<char> options,
        Encoding? encoding = null,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        return ReadAsync<TValue>(
            new StreamReader(stream, encoding: encoding, leaveOpen: leaveOpen, bufferSize: 4096),
            options,
            leaveOpen,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously reads <typeparamref name="TValue"/> from the reader.
    /// </summary>
    /// <remarks>
    /// The reader is completed at the end of the enumeration (on explicit dispose or at the end of a foreach-loop).
    /// </remarks>
    /// <param name="textReader">Text reader to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <param name="leaveOpen">
    /// If <see langword="true"/>, the writer is not disposed at the end of the enumeration
    /// </param>
    /// <param name="cancellationToken">Token to cancel the enumeration</param>
    /// <returns>
    /// <see cref="IAsyncEnumerable{T}"/> that reads records asynchronously line-by-line from the stream
    /// as it is enumerated.
    /// </returns>
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        TextReader textReader,
        CsvReaderOptions<char> options,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(options);

        if (options.HasHeader)
        {
            return ReadAsyncInternal<TValue, CsvHeaderProcessor<char, TValue>>(
                textReader,
                new CsvHeaderProcessor<char, TValue>(options),
                leaveOpen,
                cancellationToken);
        }

        return ReadAsyncInternal<TValue, CsvProcessor<char, TValue>>(
            textReader,
            new CsvProcessor<char, TValue>(options),
            leaveOpen,
            cancellationToken);
    }

    private static async IAsyncEnumerable<TValue> ReadAsyncInternal<TValue, TProcessor>(
        TextReader textReader,
        TProcessor processor,
        bool leaveOpen,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TProcessor : struct, ICsvProcessor<char, TValue>
    {
        using var reader = new TextPipeReader(textReader, 4096, leaveOpen);

        try
        {
            while (true)
            {
                TextReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<char> buffer = result.Buffer;

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
        }
    }
}
