using System.IO.Pipelines;
using CommunityToolkit.Diagnostics;
using FlameCsv.Reading;
using System.Text;
using FlameCsv.Extensions;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Enumeration;
using DAM = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute;
using RUF = System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute;

namespace FlameCsv;

/// <summary>
/// Provides static methods for reading CSV records as objects or structs.
/// </summary>
[SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Justification = "Readers are passed to an enumerable whose enumerator disposes the reader")]
public static partial class CsvReader
{
    /// <summary>
    /// Default buffer size used by the <see cref="CsvReader"/> when creating a <see cref="PipeReader"/>
    /// or a <see cref="TextReader"/>.
    /// </summary>
    public const int DefaultBufferSize = 4096;

    /// <inheritdoc cref="Read{T,TValue}(CsvOptions{T},ReadOnlyMemory{T})"/>
    [RUF(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<char, TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(
        string? csv,
        CsvOptions<char>? options = null)
    {
        return new CsvValueEnumerable<char, TValue>(
            new ReadOnlySequence<char>(csv.AsMemory()),
            options ?? CsvTextOptions.Default);
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvOptions{T},ReadOnlyMemory{T})"/>
    [RUF(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<char, TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(
        ReadOnlyMemory<char> csv,
        CsvOptions<char>? options = null)
    {
        return new CsvValueEnumerable<char, TValue>(
            new ReadOnlySequence<char>(csv),
            options ?? CsvTextOptions.Default);
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvOptions{T},ReadOnlyMemory{T})"/>
    [RUF(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<byte, TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(
        ReadOnlyMemory<byte> csv,
        CsvOptions<byte>? options = null)
    {
        return new CsvValueEnumerable<byte, TValue>(
            new ReadOnlySequence<byte>(csv),
            options ?? CsvUtf8Options.Default);
    }

    /// <summary>
    /// Synchronously reads <typeparamref name="TValue"/> from the data.
    /// </summary>
    /// <param name="csv">Data to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <returns><see cref="IEnumerable{T}"/> that reads records line-by-line from the data.</returns>
    [RUF(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<T, TValue> Read<T, [DAM(Messages.ReflectionBound)] TValue>(
        ReadOnlyMemory<T> csv,
        CsvOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<T, TValue>(new ReadOnlySequence<T>(csv), options);
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvOptions{T},ReadOnlyMemory{T})"/>
    [RUF(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<T, TValue> Read<T, [DAM(Messages.ReflectionBound)] TValue>(
        in ReadOnlySequence<T> csv,
        CsvOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<T, TValue>(in csv, options);
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
    [RUF(Messages.CompiledExpressions)]
    public static CsvValueAsyncEnumerable<char, TValue> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(
        Stream stream,
        Encoding encoding,
        CsvOptions<char>? options = null,
        bool leaveOpen = false,
        int bufferSize = DefaultBufferSize)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        options ??= CsvTextOptions.Default;
        var textReader = new StreamReader(stream, encoding: encoding, bufferSize: bufferSize, leaveOpen: leaveOpen);
        var reader = new TextPipeReader(textReader, bufferSize, options._arrayPool);
        return new CsvValueAsyncEnumerable<char, TValue>(reader, options);
    }

    /// <summary>
    /// Asynchronously reads <typeparamref name="TValue"/> from the reader.
    /// </summary>
    /// <remarks>
    /// The reader is completed at the end of the enumeration (on explicit dispose or at the end of a foreach-loop).
    /// </remarks>
    /// <param name="textReader">Text reader to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <returns><see cref="IAsyncEnumerable{T}"/> that reads the CSV one record at a time from the reader.</returns>
    [RUF(Messages.CompiledExpressions)]
    public static CsvValueAsyncEnumerable<char, TValue> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(
        TextReader textReader,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(textReader);

        options ??= CsvTextOptions.Default;
        var reader = new TextPipeReader(textReader, DefaultBufferSize, options._arrayPool);
        return new CsvValueAsyncEnumerable<char, TValue>(reader, options);
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
    [RUF(Messages.CompiledExpressions)]
    public static CsvValueAsyncEnumerable<byte, TValue> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(
        Stream stream,
        CsvOptions<byte>? options = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        options ??= CsvUtf8Options.Default;
        var reader = CreatePipeReader(stream, options._arrayPool, leaveOpen);
        return new CsvValueAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), options);
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
    [RUF(Messages.CompiledExpressions)]
    public static CsvValueAsyncEnumerable<byte, TValue> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(
        PipeReader reader,
        CsvOptions<byte>? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new CsvValueAsyncEnumerable<byte, TValue>(
            new PipeReaderWrapper(reader),
            options ?? CsvUtf8Options.Default);
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
        CsvOptions<char>? options = null)
    {
        return new CsvRecordEnumerable<char>(csv.AsMemory(), options ?? CsvTextOptions.Default);
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time from the <see cref="ReadOnlyMemory{T}{T}"/>.
    /// </summary>
    /// <inheritdoc cref="Enumerate(string?, CsvOptions{char})" path="/remarks"/>
    /// <param name="csv"><inheritdoc cref="Enumerate(string?, CsvOptions{char})" path="/param[@name='csv']"/></param>
    /// <param name="options"><inheritdoc cref="Enumerate(string?, CsvOptions{char})" path="/param[@name='options']"/></param>
    public static CsvRecordEnumerable<T> Enumerate<T>(
        ReadOnlyMemory<T> csv,
        CsvOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        return new CsvRecordEnumerable<T>(csv, options);
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time from the <see cref="ReadOnlySequence{T}"/>.
    /// </summary>
    /// <inheritdoc cref="Enumerate(string?, CsvOptions{char})" path="/remarks"/>
    /// <param name="csv"><inheritdoc cref="Enumerate(string?, CsvOptions{char})" path="/param[@name='csv']"/></param>
    /// <param name="options"><inheritdoc cref="Enumerate(string?, CsvOptions{char})" path="/param[@name='options']"/></param>
    public static CsvRecordEnumerable<T> Enumerate<T>(
        in ReadOnlySequence<T> csv,
        CsvOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        return new CsvRecordEnumerable<T>(in csv, options);
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time from the <see cref="Stream"/> by creating a
    /// <see cref="StreamReader"/> using the specified options.
    /// </summary>
    /// <inheritdoc cref="Enumerate(string?, CsvOptions{char})" path="/remarks"/>
    /// <param name="stream"></param>
    /// <param name="options"><inheritdoc cref="Enumerate(string?, CsvOptions{char})" path="/param[@name='options']"/></param>
    public static CsvRecordAsyncEnumerable<char> EnumerateAsync(
        Stream stream,
        CsvOptions<char>? options = null,
        Encoding? encoding = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        return EnumerateAsync(
            new StreamReader(stream, encoding: encoding, bufferSize: 4096, leaveOpen: leaveOpen),
            options ?? CsvTextOptions.Default);
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
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(textReader);

        options ??= CsvTextOptions.Default;
        return new CsvRecordAsyncEnumerable<char>(
            new TextPipeReader(textReader, 4096, options._arrayPool),
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
        CsvOptions<byte>? options = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);

        options ??= CsvUtf8Options.Default;
        return new CsvRecordAsyncEnumerable<byte>(
            new PipeReaderWrapper(CreatePipeReader(stream, options._arrayPool, leaveOpen)),
            options);
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
        CsvOptions<byte>? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new CsvRecordAsyncEnumerable<byte>(new PipeReaderWrapper(reader), options ?? CsvUtf8Options.Default);
    }

    /// <summary>
    /// Creates a PipeReader from a Stream.
    /// </summary>
    private static PipeReader CreatePipeReader(
        Stream stream,
        ArrayPool<byte> arrayPool,
        bool leaveOpen)
    {
        Guard.CanRead(stream);

        return PipeReader.Create(
            stream,
            new StreamPipeReaderOptions(
                pool: arrayPool.AsMemoryPool(),
                leaveOpen: leaveOpen));
    }
}
