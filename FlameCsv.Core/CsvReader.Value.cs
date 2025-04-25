using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;
using FlameCsv.IO;

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
    /// <param name="encoding">Encoding, defaulting to UTF8</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    /// <param name="ioOptions">Options to configure the inner reader</param>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    [OverloadResolutionPriority(-1)] // prefer byte overload when providing just a stream
    public static CsvValueEnumerable<char, TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(
        Stream stream,
        Encoding? encoding = null,
        CsvOptions<char>? options = null,
        CsvIOOptions ioOptions = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        options ??= CsvOptions<char>.Default;
        var reader = CsvBufferReader.Create(stream, encoding, options.Allocator, ioOptions);
        return new CsvValueEnumerable<char, TValue>(reader, options);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the stream using reflection.
    /// </summary>
    /// <param name="textReader">Text reader to read the records from</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    /// <param name="ioOptions">Options to configure the inner reader</param>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueEnumerable<char, TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(
        TextReader textReader,
        CsvOptions<char>? options = null,
        CsvIOOptions ioOptions = default)
    {
        ArgumentNullException.ThrowIfNull(textReader);

        options ??= CsvOptions<char>.Default;
        var reader = CsvBufferReader.Create(textReader, options.Allocator, ioOptions);
        return new CsvValueEnumerable<char, TValue>(reader, options);
    }

    /// <summary>
    /// Parses instances of <typeparamref name="TValue"/> from the stream using reflection.
    /// </summary>
    /// <param name="stream">Stream to read the records from</param>
    /// <param name="options">Options to use, <see cref="CsvOptions{T}.Default"/> used by default</param>
    /// <param name="ioOptions">Options to configure the inner reader</param>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static CsvValueEnumerable<byte, TValue> Read<[DAM(Messages.ReflectionBound)] TValue>(
        Stream stream,
        CsvOptions<byte>? options = null,
        CsvIOOptions ioOptions = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        options ??= CsvOptions<byte>.Default;
        var reader = CsvBufferReader.Create(stream, options.Allocator, ioOptions);
        return new CsvValueEnumerable<byte, TValue>(reader, options);
    }
}
