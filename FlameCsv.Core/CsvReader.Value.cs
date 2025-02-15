using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public static partial class CsvReader
{
    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the CSV data using reflection.
    /// </summary>
    /// <param name="csv">CSV data</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueEnumerable<char, TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(
        string? csv,
        CsvOptions<char>? options = null)
    {
        return new CsvValueEnumerable<char, TValue>(
            new ReadOnlySequence<char>(csv.AsMemory()),
            options ?? CsvOptions<char>.Default);
    }

    /// <inheritdoc cref="Read{TValue}(string?,FlameCsv.CsvOptions{char}?)"/>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueEnumerable<char, TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(
        ReadOnlyMemory<char> csv,
        CsvOptions<char>? options = null)
    {
        return new CsvValueEnumerable<char, TValue>(
            new ReadOnlySequence<char>(csv),
            options ?? CsvOptions<char>.Default);
    }

    /// <inheritdoc cref="Read{TValue}(string?,FlameCsv.CsvOptions{char}?)"/>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueEnumerable<byte, TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(
        ReadOnlyMemory<byte> csv,
        CsvOptions<byte>? options = null)
    {
        return new CsvValueEnumerable<byte, TValue>(
            new ReadOnlySequence<byte>(csv),
            options ?? CsvOptions<byte>.Default);
    }

    /// <inheritdoc cref="Read{TValue}(string?,FlameCsv.CsvOptions{char}?)"/>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    [OverloadResolutionPriority(-1)]
    public static CsvValueEnumerable<T, TValue> Read<T, [DAM(Messages.ReflectionBound)] TValue>(
        ReadOnlyMemory<T> csv,
        CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        options ??= CsvOptions<T>.Default;
        return new CsvValueEnumerable<T, TValue>(new ReadOnlySequence<T>(csv), options);
    }

    /// <inheritdoc cref="Read{TValue}(string?,FlameCsv.CsvOptions{char}?)"/>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueEnumerable<T, TValue> Read<T, [DAM(Messages.ReflectionBound)] TValue>(
        in ReadOnlySequence<T> csv,
        CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        options ??= CsvOptions<T>.Default;
        return new CsvValueEnumerable<T, TValue>(in csv, options);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the stream using reflection.
    /// </summary>
    /// <param name="stream">Stream to read the records from</param>
    /// <param name="encoding">Encoding to initialize the <see cref="StreamWriter"/> with</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    /// <param name="leaveOpen">If <see langword="true"/>, the stream is not disposed after the enumeration ends.</param>
    /// <param name="bufferSize">Optional buffer size</param>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueAsyncEnumerable<char, TValue> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(
        Stream stream,
        Encoding? encoding = null,
        CsvOptions<char>? options = null,
        bool leaveOpen = false,
        int bufferSize = -1)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        options ??= CsvOptions<char>.Default;
        using StreamReader textReader = new StreamReader(stream, encoding: encoding, bufferSize: bufferSize, leaveOpen: leaveOpen);
        ICsvPipeReader<char> reader = CreatePipeReader(textReader, options._memoryPool, bufferSize);
        return new CsvValueAsyncEnumerable<char, TValue>(reader, options);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the stream using reflection.
    /// </summary>
    /// <param name="textReader">Text reader to read the records from</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueAsyncEnumerable<char, TValue> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(
        TextReader textReader,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(textReader);

        options ??= CsvOptions<char>.Default;
        var reader = CreatePipeReader(textReader, options._memoryPool, DefaultBufferSize);
        return new CsvValueAsyncEnumerable<char, TValue>(reader, options);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the stream using reflection.
    /// </summary>
    /// <param name="stream">Stream to read the records from</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    /// <param name="leaveOpen">If <see langword="true"/>, the stream is not disposed after the enumeration ends.</param>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueAsyncEnumerable<byte, TValue> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(
        Stream stream,
        CsvOptions<byte>? options = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        options ??= CsvOptions<byte>.Default;
        var reader = CreatePipeReader(stream, options._memoryPool, leaveOpen);
        return new CsvValueAsyncEnumerable<byte, TValue>(reader, options);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the pipe using reflection.
    /// </summary>
    /// <param name="reader">Pipe to read the records from</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueAsyncEnumerable<byte, TValue> ReadAsync<[DAM(Messages.ReflectionBound)] TValue>(
        PipeReader reader,
        CsvOptions<byte>? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new CsvValueAsyncEnumerable<byte, TValue>(
            new PipeReaderWrapper(reader),
            options ?? CsvOptions<byte>.Default);
    }
}
