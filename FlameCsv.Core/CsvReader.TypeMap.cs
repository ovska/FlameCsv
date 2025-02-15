using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Reading;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;

namespace FlameCsv;

public static partial class CsvReader
{
    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the CSV data using a precompiled type map.
    /// </summary>
    /// <param name="csv">CSV data</param>
    /// <param name="typeMap">Precompiled type map</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    public static CsvTypeMapEnumerable<char, TValue> Read<TValue>(
        string? csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<char, TValue>(
            new ReadOnlySequence<char>(csv.AsMemory()),
            options ?? CsvOptions<char>.Default,
            typeMap);
    }

    /// <inheritdoc cref="Read{TValue}(string?,CsvTypeMap{char,TValue},CsvOptions{char}?)"/>
    public static CsvTypeMapEnumerable<char, TValue> Read<TValue>(
        ReadOnlyMemory<char> csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<char, TValue>(
            new ReadOnlySequence<char>(csv),
            options ?? CsvOptions<char>.Default,
            typeMap);
    }

    /// <inheritdoc cref="Read{TValue}(string?,CsvTypeMap{char,TValue},CsvOptions{char}?)"/>
    public static CsvTypeMapEnumerable<byte, TValue> Read<TValue>(
        ReadOnlyMemory<byte> csv,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<byte, TValue>(
            new ReadOnlySequence<byte>(csv),
            options ?? CsvOptions<byte>.Default,
            typeMap);
    }

    /// <inheritdoc cref="Read{TValue}(string?,CsvTypeMap{char,TValue},CsvOptions{char}?)"/>
    public static CsvTypeMapEnumerable<char, TValue> Read<TValue>(
        in ReadOnlySequence<char> csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<char, TValue>(in csv, options, typeMap);
    }

    /// <inheritdoc cref="Read{TValue}(string?,CsvTypeMap{char,TValue},CsvOptions{char}?)"/>
    public static CsvTypeMapEnumerable<byte, TValue> Read<TValue>(
        in ReadOnlySequence<byte> csv,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<byte, TValue>(in csv, options, typeMap);
    }

    /// <inheritdoc cref="Read{TValue}(string?,CsvTypeMap{char,TValue},CsvOptions{char}?)"/>
    [OverloadResolutionPriority(-1)]
    public static CsvTypeMapEnumerable<T, TValue> Read<T, TValue>(
        ReadOnlyMemory<T> csv,
        CsvTypeMap<T, TValue> typeMap,
        CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<T, TValue>(new ReadOnlySequence<T>(csv), options, typeMap);
    }

    /// <inheritdoc cref="Read{TValue}(string?,CsvTypeMap{char,TValue},CsvOptions{char}?)"/>
    [OverloadResolutionPriority(-1)]
    public static CsvTypeMapEnumerable<T, TValue> Read<T, TValue>(
        in ReadOnlySequence<T> csv,
        CsvTypeMap<T, TValue> typeMap,
        CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<T, TValue>(in csv, options, typeMap);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the stream using a precompiled type map.
    /// </summary>
    /// <param name="stream">Stream to read the records from</param>
    /// <param name="typeMap">Precompiled type map</param>
    /// <param name="encoding">Encoding to initialize the <see cref="StreamWriter"/> with</param>
    /// <param name="options"><inheritdoc cref="Read{TValue}(string?,FlameCsv.CsvOptions{char}?)" path="/param[@name='options']"/></param>
    /// <param name="leaveOpen">If <see langword="true"/>, the stream is not disposed after the enumeration ends.</param>
    /// <param name="bufferSize">Optional buffer size</param>
    public static CsvTypeMapAsyncEnumerable<char, TValue> ReadAsync<TValue>(
        Stream stream,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null,
        Encoding? encoding = null,
        bool leaveOpen = false,
        int bufferSize = -1)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(typeMap);
        Guard.CanRead(stream);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        options ??= CsvOptions<char>.Default;
        StreamReader textReader = new(stream, encoding: encoding, bufferSize: bufferSize, leaveOpen: leaveOpen);
        ICsvPipeReader<char> reader = CreatePipeReader(textReader, options._memoryPool, bufferSize);
        return new CsvTypeMapAsyncEnumerable<char, TValue>(
            reader,
            options,
            typeMap);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the text reader using a precompiled type map.
    /// </summary>
    /// <param name="textReader">Text reader to read the records from</param>
    /// <param name="typeMap">Precompiled type map</param>
    /// <param name="options"><inheritdoc cref="Read{TValue}(string?,FlameCsv.CsvOptions{char}?)" path="/param[@name='options']"/></param>
    public static CsvTypeMapAsyncEnumerable<char, TValue> ReadAsync<TValue>(
        TextReader textReader,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(typeMap);

        options ??= CsvOptions<char>.Default;
        ICsvPipeReader<char> reader = CreatePipeReader(textReader, options._memoryPool, DefaultBufferSize);
        return new CsvTypeMapAsyncEnumerable<char, TValue>(reader, options, typeMap);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the stream using a precompiled type map.
    /// </summary>
    /// <param name="stream">Stream to read the records from</param>
    /// <param name="typeMap">Precompiled type map</param>
    /// <param name="options"><inheritdoc cref="Read{TValue}(string?,FlameCsv.CsvOptions{char}?)" path="/param[@name='options']"/></param>
    /// <param name="leaveOpen">If <see langword="true"/>, the stream is not disposed after the enumeration ends.</param>
    public static CsvTypeMapAsyncEnumerable<byte, TValue> ReadAsync<TValue>(
        Stream stream,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(typeMap);
        Guard.CanRead(stream);

        options ??= CsvOptions<byte>.Default;
        var reader = CreatePipeReader(stream, options._memoryPool, leaveOpen);
        return new CsvTypeMapAsyncEnumerable<byte, TValue>(reader, options, typeMap);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the pipe using a precompiled type map.
    /// </summary>
    /// <param name="reader">Pipe to read the records from</param>
    /// <param name="typeMap">Precompiled type map</param>
    /// <param name="options"><inheritdoc cref="Read{TValue}(string?,FlameCsv.CsvOptions{char}?)" path="/param[@name='options']"/></param>
    public static CsvTypeMapAsyncEnumerable<byte, TValue> ReadAsync<TValue>(
        PipeReader reader,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(typeMap);

        return new CsvTypeMapAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), options ?? CsvOptions<byte>.Default, typeMap);
    }
}
