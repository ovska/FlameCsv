using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Reading;

namespace FlameCsv;

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
    /// <returns><see cref="IAsyncEnumerable{T}"/> that reads the CSV one record at a time from the reader.</returns>
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        Stream stream,
        CsvReaderOptions<char> options,
        Encoding? encoding = null,
        bool leaveOpen = false,
        int bufferSize = DefaultBufferSize)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        var textReader = new StreamReader(stream, encoding: encoding, leaveOpen: leaveOpen, bufferSize: bufferSize);
        var reader = new TextPipeReader(textReader, bufferSize, options.ArrayPool, leaveOpen);
        return new AsyncCsvRecordEnumerable<char, TValue, TextPipeReaderWrapper>(options, new TextPipeReaderWrapper(reader));
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
    /// <returns><see cref="IAsyncEnumerable{T}"/> that reads the CSV one record at a time from the reader.</returns>
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        TextReader textReader,
        CsvReaderOptions<char> options,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(options);

        var reader = new TextPipeReader(textReader, DefaultBufferSize, options.ArrayPool, leaveOpen);
        return new AsyncCsvRecordEnumerable<char, TValue, TextPipeReaderWrapper>(options, new TextPipeReaderWrapper(reader));
    }
}
