using System.Buffers;
using CommunityToolkit.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Reading;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;

namespace FlameCsv;

public static partial class CsvReader
{
    public static CsvTypeMapEnumerable<char, TValue> Read<TValue>(
        string? csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<char, TValue>(
            new ReadOnlySequence<char>(csv.AsMemory()),
            options ?? CsvTextOptions.Default,
            typeMap);
    }

    public static CsvTypeMapEnumerable<char, TValue> Read<TValue>(
        ReadOnlyMemory<char> csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<char, TValue>(
            new ReadOnlySequence<char>(csv),
            options ?? CsvTextOptions.Default,
            typeMap);
    }

    public static CsvTypeMapEnumerable<byte, TValue> Read<TValue>(
        ReadOnlyMemory<byte> csv,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null)
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<byte, TValue>(
            new ReadOnlySequence<byte>(csv),
            options ?? CsvUtf8Options.Default,
            typeMap);
    }

    public static CsvTypeMapEnumerable<T, TValue> Read<T, TValue>(
        ReadOnlyMemory<T> csv,
        CsvTypeMap<T, TValue> typeMap,
        CsvOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<T, TValue>(new ReadOnlySequence<T>(csv), options, typeMap);
    }

    public static CsvTypeMapEnumerable<T, TValue> Read<T, TValue>(
        in ReadOnlySequence<T> csv,
        CsvTypeMap<T, TValue> typeMap,
        CsvOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<T, TValue>(in csv, options, typeMap);
    }

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

        options ??= CsvTextOptions.Default;
        var textReader = new StreamReader(stream, encoding: encoding, bufferSize: bufferSize, leaveOpen: leaveOpen);
        var reader = new TextPipeReader(textReader, bufferSize, options._arrayPool);
        return new CsvTypeMapAsyncEnumerable<char, TValue>(
            reader,
            options,
            typeMap);
    }

    public static CsvTypeMapAsyncEnumerable<char, TValue> ReadAsync<TValue>(
        TextReader textReader,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char>? options = null)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(typeMap);

        options ??= CsvTextOptions.Default;
        var reader = new TextPipeReader(textReader, DefaultBufferSize, options._arrayPool);
        return new CsvTypeMapAsyncEnumerable<char, TValue>(reader, options, typeMap);
    }

    public static CsvTypeMapAsyncEnumerable<byte, TValue> ReadAsync<TValue>(
        Stream stream,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(typeMap);
        Guard.CanRead(stream);

        options ??= CsvUtf8Options.Default;
        var reader = CreatePipeReader(stream, options._arrayPool, leaveOpen);
        return new CsvTypeMapAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), options, typeMap);
    }

    public static CsvTypeMapAsyncEnumerable<byte, TValue> ReadAsync<TValue>(
        PipeReader reader,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte>? options = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(typeMap);

        return new CsvTypeMapAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), options ?? CsvUtf8Options.Default, typeMap);
    }
}
