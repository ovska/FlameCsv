using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.IO;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Provides a static factory method for creating <see cref="CsvParser{T}"/> instances.
/// </summary>
[PublicAPI]
public abstract class CsvParser
{
    private static readonly int _fieldBufferLength = Messages.ReadAheadCount;

    [ThreadStatic] private static Meta[]? StaticMetaBuffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static Meta[] GetMetaBuffer()
    {
        Meta[] array = StaticMetaBuffer ?? new Meta[_fieldBufferLength];
        StaticMetaBuffer = null;
        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static void ReturnMetaBuffer(ref Meta[] array)
    {
        // return the buffer to the thread-static unless someone read over a thousand fields into it
        if (array.Length == _fieldBufferLength)
        {
            StaticMetaBuffer ??= array;
        }
    }

    /// <summary>
    /// Creates a new instance of a CSV parser.
    /// </summary>
    /// <param name="options">Options-instance that determines the dialect and memory pool to use</param>
    /// <param name="reader">Reader for the CSV data</param>
    [MustDisposeResource]
    public static CsvParser<T> Create<T>(CsvOptions<T> options, ICsvPipeReader<T> reader)
        where T : unmanaged, IBinaryInteger<T>
    {
        return CreateCore(options, reader);
    }

    /// <summary>
    /// Creates a new instance of a CSV parser.
    /// </summary>
    /// <param name="options">Options-instance that determines the dialect and memory pool to use</param>
    /// <param name="csv">CSV data</param>
    [MustDisposeResource]
    public static CsvParser<T> Create<T>(CsvOptions<T> options, in ReadOnlySequence<T> csv)
        where T : unmanaged, IBinaryInteger<T>
    {
        return CreateCore(options, CsvPipeReader.Create(in csv));
    }

    [MustDisposeResource]
    internal static CsvParser<T> CreateCore<T>(
        CsvOptions<T> options,
        ICsvPipeReader<T> reader,
        CsvParserOptions<T> parserOptions = default)
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);

        options.MakeReadOnly();
        return !options.Dialect.Escape.HasValue
            ? new CsvParserRFC4180<T>(options, reader, in parserOptions)
            : new CsvParserUnix<T>(options, reader, in parserOptions);
    }

    private protected CsvParser()
    {
    }

    // ReSharper disable NotAccessedField.Global
    private protected struct MetaSegment
    {
        public Meta[]? array;
        public int offset;
        public int count;

#if DEBUG
        static MetaSegment()
        {
            if (Unsafe.SizeOf<MetaSegment>() != Unsafe.SizeOf<ArraySegment<Meta>>())
            {
                throw new InvalidOperationException("MetaSegment has unexpected size");
            }

            var array = new Meta[4];
            array[1] = Meta.StartOfData;
            var segment = new MetaSegment { array = array, offset = 1, count = 2 };
            var cast = Unsafe.As<MetaSegment, ArraySegment<Meta>>(ref segment);
            Debug.Assert(cast.Array == array);
            Debug.Assert(cast.Offset == 1);
            Debug.Assert(cast.Count == 2);
        }
#endif
    }
    // ReSharper restore NotAccessedField.Global
}
