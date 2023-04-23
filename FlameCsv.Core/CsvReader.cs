using System.IO.Pipelines;
using CommunityToolkit.Diagnostics;
using FlameCsv.Enumeration;
using FlameCsv.Reading;
using System.Text;
using FlameCsv.Extensions;
using System.Buffers;
using System.Diagnostics;

namespace FlameCsv;

/// <summary>
/// Provides static methods for reading CSV records as objects or structs.
/// </summary>
public static class CsvReader
{
    /// <summary>
    /// Default buffer size used by the <see cref="CsvReader"/> when creating a <see cref="PipeReader"/>
    /// or a <see cref="TextReader"/>.
    /// </summary>
    public const int DefaultBufferSize = 4096;

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static CsvValueEnumerable<char, TValue> Read<TValue>(
        string? csv,
        CsvReaderOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<char, TValue>(options, new ReadOnlySequence<char>(csv.AsMemory()));
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static CsvValueEnumerable<char, TValue> Read<TValue>(
        ReadOnlyMemory<char> csv,
        CsvReaderOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<char, TValue>(options, new ReadOnlySequence<char>(csv));
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static CsvValueEnumerable<byte, TValue> Read<TValue>(
        ReadOnlyMemory<byte> csv,
        CsvReaderOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<byte, TValue>(options, new ReadOnlySequence<byte>(csv));
    }

    /// <summary>
    /// Synchronously reads <typeparamref name="TValue"/> from the data.
    /// </summary>
    /// <param name="csv">Data to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <returns><see cref="IEnumerable{T}"/> that reads records line-by-line from the data.</returns>
    public static CsvValueEnumerable<T, TValue> Read<T, TValue>(
        ReadOnlyMemory<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<T, TValue>(options, new ReadOnlySequence<T>(csv));
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    public static CsvValueEnumerable<T, TValue> Read<T, TValue>(
        ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<T, TValue>(options, csv);
    }

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
        return new AsyncCsvValueEnumerable<char, TValue, TextPipeReaderWrapper>(options, new TextPipeReaderWrapper(reader));
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
        return new AsyncCsvValueEnumerable<char, TValue, TextPipeReaderWrapper>(options, new TextPipeReaderWrapper(reader));
    }

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
        return new AsyncCsvValueEnumerable<byte, TValue, PipeReaderWrapper>(options, new PipeReaderWrapper(reader));
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

        return new AsyncCsvValueEnumerable<byte, TValue, PipeReaderWrapper>(options, new PipeReaderWrapper(reader));
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time from the CSV <see langword="string"/>.
    /// </summary>
    /// <remarks>
    /// The return value is intended to be used directly in a <c>foreach</c>-loop The records returned from the
    /// enumerator share their memory between the input and each other. The returned types intentionally do not
    /// implement the enumerable-interfaces to only make them usable in a <c>foreach</c>-loop via duck typing.
    /// If you need to preserve the records for later use, or wish to use a LINQ-queries on the returned values,
    /// you should convert the records to <see cref="CsvRecord{T}"/>, which makes a copy of each record's data,
    /// making them safe for later use.
    /// </remarks>
    /// <param name="csv">CSV data to read the records from</param>
    /// <param name="options">Options instance defining the dialect and parsers</param>
    public static CsvRecordEnumerable<char> Enumerate(
        string? csv,
        CsvReaderOptions<char> options)
    {
        return new CsvRecordEnumerable<char>(csv.AsMemory(), options);
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time from the <see cref="ReadOnlyMemory{T}{T}"/>.
    /// </summary>
    /// <inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/remarks"/>
    /// <param name="csv"><inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/param[@name='csv']"/></param>
    /// <param name="options"><inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/param[@name='options']"/></param>
    public static CsvRecordEnumerable<T> Enumerate<T>(
        ReadOnlyMemory<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        return new CsvRecordEnumerable<T>(csv, options);
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time from the <see cref="ReadOnlySequence{T}"/>.
    /// </summary>
    /// <inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/remarks"/>
    /// <param name="csv"><inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/param[@name='csv']"/></param>
    /// <param name="options"><inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/param[@name='options']"/></param>
    public static CsvRecordEnumerable<T> Enumerate<T>(
        ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        return new CsvRecordEnumerable<T>(csv, options);
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time from the <see cref="Stream"/> by creating a
    /// <see cref="StreamReader"/> using the specified options.
    /// </summary>
    /// <inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/remarks"/>
    /// <param name="stream"></param>
    /// <param name="options"><inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/param[@name='options']"/></param>
    public static CsvRecordAsyncEnumerable<char> EnumerateAsync(
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
    /// Returns an enumerable that reads one record at a time asynchronously from the reader.
    /// </summary>
    /// <remarks>
    /// The return value is intended to be used directly in a <c>await foreach</c>-loop. The records returned from the
    /// enumerator share their memory between the input and each other. If you need to preserve the records for later use,
    /// or wish to use a LINQ-query such as <c>First()</c> on the returned value, you should convert the records
    /// <see cref="CsvRecord{T}"/>, which makes a copy of each record's data, making it safe for later use.
    /// </remarks>
    public static CsvRecordAsyncEnumerable<char> EnumerateAsync(
        TextReader textReader,
        CsvReaderOptions<char> options,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(options);

        return new CsvRecordAsyncEnumerable<char>(
            new TextPipeReader(textReader, 4096, options.ArrayPool, leaveOpen: leaveOpen),
            options);
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time asynchronously from the stream.
    /// </summary>
    /// <remarks>
    /// The return value is intended to be used directly in a <c>await foreach</c>-loop. The records returned from the
    /// enumerator share their memory between the input and each other. If you need to preserve the records for later use,
    /// or wish to use a LINQ-query such as <c>First()</c> on the returned value, you should convert the records
    /// <see cref="CsvRecord{T}"/>, which makes a copy of each record's data, making it safe for later use.
    /// </remarks>
    public static CsvRecordAsyncEnumerable<byte> EnumerateAsync(
        Stream stream,
        CsvReaderOptions<byte> options,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        return EnumerateAsync(CreatePipeReader(stream, options, leaveOpen), options);
    }


    /// <summary>
    /// Returns an enumerable that reads one record at a time asynchronously from the reader.
    /// </summary>
    /// <remarks>
    /// The return value is intended to be used directly in a <c>await foreach</c>-loop. The records returned from the
    /// enumerator share their memory between the input and each other. If you need to preserve the records for later use,
    /// or wish to use a LINQ-query such as <c>First()</c> on the returned value, you should convert the records
    /// <see cref="CsvRecord{T}"/>, which makes a copy of each record's data, making it safe for later use.
    /// </remarks>
    public static CsvRecordAsyncEnumerable<byte> EnumerateAsync(
        PipeReader reader,
        CsvReaderOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);

        return new CsvRecordAsyncEnumerable<byte>(new PipeReaderWrapper(reader), options);
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
