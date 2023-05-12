using System.IO.Pipelines;
using CommunityToolkit.Diagnostics;
using FlameCsv.Reading;
using System.Text;
using FlameCsv.Extensions;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Enumeration;

namespace FlameCsv;

/// <summary>
/// Provides static methods for reading CSV records as objects or structs.
/// </summary>
[SuppressMessage(
    "Reliability",
    "CA2000:Dispose objects before losing scope",
    Justification = "Readers are passed to an enumerable whose enumerator disposes the reader")]
[SuppressMessage(
    "Roslynator",
    "RCS1047:Non-asynchronous method name should not end with 'Async'.",
    Justification = "Method returns a duck typed IAsyncEnumerable")]
public static partial class CsvReader
{
    /// <summary>
    /// Default buffer size used by the <see cref="CsvReader"/> when creating a <see cref="PipeReader"/>
    /// or a <see cref="TextReader"/>.
    /// </summary>
    public const int DefaultBufferSize = 4096;

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<char, TValue> Read<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        string? csv,
        CsvReaderOptions<char> options,
        CsvContextOverride<char> context = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<char, TValue>(new ReadOnlySequence<char>(csv.AsMemory()), options, context);
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<char, TValue> Read<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        ReadOnlyMemory<char> csv,
        CsvReaderOptions<char> options,
        CsvContextOverride<char> context = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<char, TValue>(new ReadOnlySequence<char>(csv), options, context);
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<byte, TValue> Read<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        ReadOnlyMemory<byte> csv,
        CsvReaderOptions<byte> options,
        CsvContextOverride<byte> context = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<byte, TValue>(new ReadOnlySequence<byte>(csv), options, context);
    }

    /// <summary>
    /// Synchronously reads <typeparamref name="TValue"/> from the data.
    /// </summary>
    /// <param name="csv">Data to read the records from</param>
    /// <param name="options">Options instance containing tokens and parsers</param>
    /// <returns><see cref="IEnumerable{T}"/> that reads records line-by-line from the data.</returns>
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<T, TValue> Read<T, [DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        ReadOnlyMemory<T> csv,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> context = default)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<T, TValue>(new ReadOnlySequence<T>(csv), options, context);
    }

    /// <inheritdoc cref="Read{T,TValue}(CsvReaderOptions{T},ReadOnlyMemory{T})"/>
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static CsvValueEnumerable<T, TValue> Read<T, [DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> context = default)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return new CsvValueEnumerable<T, TValue>(csv, options, context);
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
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static CsvValueAsyncEnumerable<char, TValue> ReadAsync<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        Stream stream,
        CsvReaderOptions<char> options,
        CsvContextOverride<char> context = default,
        Encoding? encoding = null,
        bool leaveOpen = false,
        int bufferSize = DefaultBufferSize)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        var readerContext = new CsvReadingContext<char>(options, context);
        var textReader = new StreamReader(stream, encoding: encoding, leaveOpen: leaveOpen, bufferSize: bufferSize);
        var reader = new TextPipeReader(textReader, bufferSize, readerContext.ArrayPool);
        return new CsvValueAsyncEnumerable<char, TValue>(reader, in readerContext);
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
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static CsvValueAsyncEnumerable<char, TValue> ReadAsync<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        TextReader textReader,
        CsvReaderOptions<char> options,
        CsvContextOverride<char> context = default)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(options);

        var readerContext = new CsvReadingContext<char>(options, context);
        var reader = new TextPipeReader(textReader, DefaultBufferSize, readerContext.ArrayPool);
        return new CsvValueAsyncEnumerable<char, TValue>(reader, in readerContext);
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
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static CsvValueAsyncEnumerable<byte, TValue> ReadAsync<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        Stream stream,
        CsvReaderOptions<byte> options,
        CsvContextOverride<byte> context = default,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        var readingContext = new CsvReadingContext<byte>(options, context);
        var reader = CreatePipeReader(stream, in readingContext, leaveOpen);
        return new CsvValueAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), in readingContext);
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
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static CsvValueAsyncEnumerable<byte, TValue> ReadAsync<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        PipeReader reader,
        CsvReaderOptions<byte> options,
        CsvContextOverride<byte> context = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);

        return new CsvValueAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), new CsvReadingContext<byte>(options, context));
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
        CsvReaderOptions<char> options,
        CsvContextOverride<char> context = default)
    {
        return new CsvRecordEnumerable<char>(csv.AsMemory(), options, context);
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time from the <see cref="ReadOnlyMemory{T}{T}"/>.
    /// </summary>
    /// <inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/remarks"/>
    /// <param name="csv"><inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/param[@name='csv']"/></param>
    /// <param name="options"><inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/param[@name='options']"/></param>
    public static CsvRecordEnumerable<T> Enumerate<T>(
        ReadOnlyMemory<T> csv,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> context = default)
        where T : unmanaged, IEquatable<T>
    {
        return new CsvRecordEnumerable<T>(csv, options, context);
    }

    /// <summary>
    /// Returns an enumerable that reads one record at a time from the <see cref="ReadOnlySequence{T}"/>.
    /// </summary>
    /// <inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/remarks"/>
    /// <param name="csv"><inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/param[@name='csv']"/></param>
    /// <param name="options"><inheritdoc cref="Enumerate(string?, CsvReaderOptions{char})" path="/param[@name='options']"/></param>
    public static CsvRecordEnumerable<T> Enumerate<T>(
        in ReadOnlySequence<T> csv,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> context = default)
        where T : unmanaged, IEquatable<T>
    {
        return new CsvRecordEnumerable<T>(in csv, options, context);
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
        CsvContextOverride<char> context = default,
        Encoding? encoding = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        return EnumerateAsync(
            new StreamReader(stream, encoding: encoding, leaveOpen: leaveOpen, bufferSize: 4096),
            options,
            context);
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
        CsvContextOverride<char> context = default)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(options);

        var readerContext = new CsvReadingContext<char>(options, context);
        return new CsvRecordAsyncEnumerable<char>(
            new TextPipeReader(textReader, 4096, readerContext.ArrayPool),
            in readerContext);
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
        CsvContextOverride<byte> context = default,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        var readerContext = new CsvReadingContext<byte>(options, context);

        return new CsvRecordAsyncEnumerable<byte>(
            new PipeReaderWrapper(CreatePipeReader(stream, in readerContext, leaveOpen)),
            in readerContext);
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
        CsvReaderOptions<byte> options,
        CsvContextOverride<byte> context = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(options);

        return new CsvRecordAsyncEnumerable<byte>(
            new PipeReaderWrapper(reader),
            new CsvReadingContext<byte>(options, context));
    }

    /// <summary>
    /// Creates a PipeReader from a Stream.
    /// </summary>
    [StackTraceHidden]
    private static PipeReader CreatePipeReader(
        Stream stream,
        in CsvReadingContext<byte> context,
        bool leaveOpen)
    {
        Guard.CanRead(stream);

        MemoryPool<byte>? memoryPool = null;

        if (context.ArrayPool != ArrayPool<byte>.Shared)
        {
            memoryPool = context.ArrayPool.AllocatingIfNull().AsMemoryPool();
        }

        return PipeReader.Create(
            stream,
            memoryPool is null && !leaveOpen
                ? null
                : new StreamPipeReaderOptions(pool: memoryPool, leaveOpen: leaveOpen));
    }
}
