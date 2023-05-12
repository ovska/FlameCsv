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
        CsvReaderOptions<char> options,
        CsvContextOverride<char> context = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<char, TValue>(new ReadOnlySequence<char>(csv.AsMemory()), options, context, typeMap);
    }

    public static CsvTypeMapEnumerable<char, TValue> Read<TValue>(
        ReadOnlyMemory<char> csv,
        CsvTypeMap<char, TValue> typeMap,
        CsvReaderOptions<char> options,
        CsvContextOverride<char> context = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<char, TValue>(new ReadOnlySequence<char>(csv), options, context, typeMap);
    }

    public static CsvTypeMapEnumerable<byte, TValue> Read<TValue>(
        ReadOnlyMemory<byte> csv,
        CsvTypeMap<byte, TValue> typeMap,
        CsvReaderOptions<byte> options,
        CsvContextOverride<byte> context = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<byte, TValue>(new ReadOnlySequence<byte>(csv), options, context, typeMap);
    }

    public static CsvTypeMapEnumerable<T, TValue> Read<T, TValue>(
        ReadOnlyMemory<T> csv,
        CsvTypeMap<T, TValue> typeMap,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> context = default)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<T, TValue>(new ReadOnlySequence<T>(csv), options, context, typeMap);
    }

    public static CsvTypeMapEnumerable<T, TValue> Read<T, TValue>(
        ReadOnlySequence<T> csv,
        CsvTypeMap<T, TValue> typeMap,
        CsvReaderOptions<T> options,
        CsvContextOverride<T> context = default)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(typeMap);
        return new CsvTypeMapEnumerable<T, TValue>(in csv, options, context, typeMap);
    }

    public static CsvTypeMapAsyncEnumerable<char, TValue> ReadAsync<TValue>(
        Stream stream,
        CsvTypeMap<char, TValue> typeMap,
        CsvReaderOptions<char> options,
        CsvContextOverride<char> context = default,
        Encoding? encoding = null,
        bool leaveOpen = false,
        int bufferSize = DefaultBufferSize)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        var readerContext = new CsvReadingContext<char>(options, context);
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
        CsvReaderOptions<char> options,
        CsvContextOverride<char> context = default)
    {
        ArgumentNullException.ThrowIfNull(textReader);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);

        var readerContext = new CsvReadingContext<char>(options, context);
        var reader = new TextPipeReader(textReader, DefaultBufferSize, readerContext.ArrayPool);
        return new CsvTypeMapAsyncEnumerable<char, TValue>(reader, in readerContext, typeMap);
    }

    public static CsvTypeMapAsyncEnumerable<byte, TValue> ReadAsync<TValue>(
        Stream stream,
        CsvTypeMap<byte, TValue> typeMap,
        CsvReaderOptions<byte> options,
        CsvContextOverride<byte> context = default,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);
        Guard.CanRead(stream);

        var readingContext = new CsvReadingContext<byte>(options, context);
        var reader = CreatePipeReader(stream, in readingContext, leaveOpen);
        return new CsvTypeMapAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), in readingContext, typeMap);
    }

    public static CsvTypeMapAsyncEnumerable<byte, TValue> ReadAsync<TValue>(
        PipeReader reader,
        CsvTypeMap<byte, TValue> typeMap,
        CsvReaderOptions<byte> options,
        CsvContextOverride<byte> context = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(typeMap);
        ArgumentNullException.ThrowIfNull(options);

        return new CsvTypeMapAsyncEnumerable<byte, TValue>(new PipeReaderWrapper(reader), new CsvReadingContext<byte>(options, context), typeMap);
    }
}
