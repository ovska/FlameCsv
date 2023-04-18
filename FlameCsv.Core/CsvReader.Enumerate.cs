using System.Buffers;
using CommunityToolkit.Diagnostics;
using System.Text;
using FlameCsv.Reading;
using FlameCsv.Extensions;
using System.IO.Pipelines;

namespace FlameCsv;

public static partial class CsvReader
{
    /// <inheritdoc cref="Enumerate{T}(ReadOnlySequence{T},CsvReaderOptions{T})"/>
    public static CsvEnumerator<char> Enumerate(
        string? csv,
        CsvReaderOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvEnumerator<char>(new(csv.AsMemory()), options, null);
    }

    public static CsvEnumerator<byte> Enumerate(
        ReadOnlyMemory<byte> csv,
        CsvReaderOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvEnumerator<byte>(new(csv), options, null);
    }

    /// <inheritdoc cref="Enumerate{T}(ReadOnlySequence{T},CsvReaderOptions{T})"/>
    public static CsvEnumerator<T> Enumerate<T>(
        ReadOnlyMemory<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvEnumerator<T>(new ReadOnlySequence<T>(csv), options, null);
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
    public static CsvEnumerator<T> Enumerate<T>(
        ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);

        return new CsvEnumerator<T>(csv, options, null);
    }

    public static AsyncCsvEnumerator<char> EnumerateAsync(
        Stream stream,
        CsvReaderOptions<char> options,
        Encoding? encoding = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        return EnumerateAsync(
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
    public static AsyncCsvEnumerator<char> EnumerateAsync(
        TextReader textReader,
        CsvReaderOptions<char> options,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(options);

        return new AsyncCsvEnumerator<char>(
            new TextPipeReader(textReader, 4096, options.ArrayPool, leaveOpen: leaveOpen),
            options,
            columnCount: null);
    }

    public static AsyncCsvEnumerator<byte> EnumerateAsync(
        Stream stream,
        CsvReaderOptions<byte> options,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        return EnumerateAsync(CreatePipeReader(stream, options, leaveOpen), options);
    }

    public static AsyncCsvEnumerator<byte> EnumerateAsync(
        PipeReader reader,
        CsvReaderOptions<byte> options)
    {
        return new AsyncCsvEnumerator<byte>(new PipeReaderWrapper(reader), options, null);
    }
}
