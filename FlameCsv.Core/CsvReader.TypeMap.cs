using System.Buffers;
using CommunityToolkit.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Reading;
using FlameCsv.Enumeration;

namespace FlameCsv;

public static partial class CsvReader
{
    public static CsvTypeMapEnumerable<char, TValue> Read<TValue>(
        string? csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<char, TValue>(new ReadOnlySequence<char>(csv.AsMemory()), options, typeMap);
    }

    public static CsvTypeMapEnumerable<char, TValue> Read<TValue>(
        ReadOnlyMemory<char> csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<char, TValue>(new ReadOnlySequence<char>(csv), options, typeMap);
    }

    public static CsvTypeMapEnumerable<byte, TValue> Read<TValue>(
        ReadOnlyMemory<byte> csv,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<byte, TValue>(new ReadOnlySequence<byte>(csv), options, typeMap);
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
        CsvOptions<char> options,
        Encoding? encoding = null,
        bool leaveOpen = false,
        int bufferSize = -1)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        if (bufferSize != -1)
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 1);

        var readerContext = new CsvReadingContext<char>(options);
        var textReader = new StreamReader(stream, encoding: encoding, leaveOpen: leaveOpen, bufferSize: bufferSize);
        var reader = new TextPipeReader(textReader, bufferSize, readerContext.ArrayPool);
        return new CsvTypeMapAsyncEnumerable<char, TValue>(
            reader,
            in readerContext,
            typeMap);
    }

    public static CsvTypeMapAsyncEnumerable<char, TValue> ReadAsync<TValue>(
        TextReader textReader,
        CsvTypeMap<char, TValue> typeMap,
        CsvOptions<char> options)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);

        var readerContext = new CsvReadingContext<char>(options);
        var reader = new TextPipeReader(textReader, DefaultBufferSize, readerContext.ArrayPool);
        return new CsvTypeMapAsyncEnumerable<char, TValue>(reader, in readerContext, typeMap);
    }

    public static CsvTypeMapAsyncEnumerable<byte, TValue> ReadAsync<TValue>(
        Stream stream,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte> options,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        var readingContext = new CsvReadingContext<byte>(options);
        var reader = CreatePipeReader(stream, in readingContext, leaveOpen);
        return new CsvTypeMapAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), in readingContext, typeMap);
    }

    public static CsvTypeMapAsyncEnumerable<byte, TValue> ReadAsync<TValue>(
        PipeReader reader,
        CsvTypeMap<byte, TValue> typeMap,
        CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);

        return new CsvTypeMapAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), new CsvReadingContext<byte>(options), typeMap);
    }
}
