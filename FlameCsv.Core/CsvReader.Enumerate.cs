using System.Buffers;
using CommunityToolkit.Diagnostics;
using System.Text;
using FlameCsv.Reading;
using System.IO.Pipelines;

namespace FlameCsv;

public static partial class CsvReader
{
    /// <inheritdoc cref="GetEnumerable{T}(ReadOnlySequence{T},CsvReaderOptions{T})"/>
    public static CsvEnumerable<char> GetEnumerable(
        string? csv,
        CsvReaderOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvEnumerable<char>(csv.AsMemory(), options);
    }

    public static CsvEnumerable<byte> GetEnumerable(
        ReadOnlyMemory<byte> csv,
        CsvReaderOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvEnumerable<byte>(csv, options);
    }

    /// <inheritdoc cref="GetEnumerable{T}(ReadOnlySequence{T},CsvReaderOptions{T})"/>
    public static CsvEnumerable<T> GetEnumerable<T>(
        ReadOnlyMemory<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvEnumerable<T>(csv, options);
    }

    /// <summary>
    /// Returns an enumerator that can be used to read CSV records in a forward-only fashion.
    /// </summary>
    /// <remarks>
    /// The enumerator should either be used in a <see langword="foreach"/>-block or disposed explicitly.
    /// </remarks>
    /// <param name="csv">Data to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <returns>A CSV-enumerator structure</returns>
    public static CsvEnumerable<T> GetEnumerable<T>(
        ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvEnumerable<T>(csv, options);
    }

    public static AsyncCsvEnumerable<char> GetAsyncEnumerable(
        Stream stream,
        CsvReaderOptions<char> options,
        Encoding? encoding = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        return GetAsyncEnumerable(
            new StreamReader(stream, encoding: encoding, leaveOpen: leaveOpen, bufferSize: 4096),
            options,
            leaveOpen);
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
    /// <returns>
    /// <see cref="IAsyncEnumerable{T}"/> that reads records asynchronously line-by-line from the stream
    /// as it is enumerated.
    /// </returns>
    public static AsyncCsvEnumerable<char> GetAsyncEnumerable(
        TextReader textReader,
        CsvReaderOptions<char> options,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(options);

        return new AsyncCsvEnumerable<char>(
            new TextPipeReader(textReader, 4096, options.ArrayPool, leaveOpen: leaveOpen),
            options);
    }

    public static AsyncCsvEnumerable<byte> GetAsyncEnumerable(
        Stream stream,
        CsvReaderOptions<byte> options,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        return GetAsyncEnumerable(CreatePipeReader(stream, options, leaveOpen), options);
    }

    public static AsyncCsvEnumerable<byte> GetAsyncEnumerable(
        PipeReader reader,
        CsvReaderOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);

        return new AsyncCsvEnumerable<byte>(new PipeReaderWrapper(reader), options);
    }
}
