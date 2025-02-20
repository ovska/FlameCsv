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
    // TODO: profile and adjust
    private protected const int BufferedFields = 4096;

    [ThreadStatic] private static Meta[]? StaticMetaBuffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static Meta[] GetMetaBuffer()
    {
        Meta[] array = StaticMetaBuffer ?? new Meta[BufferedFields];
        StaticMetaBuffer = null;
        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static void ReturnMetaBuffer(ref Meta[] array)
    {
        // return the buffer to the thread-static unless someone read over a thousand fields into it
        if (array.Length == BufferedFields)
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
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);

        options.MakeReadOnly();
        return !options.Dialect.Escape.HasValue
            ? new CsvParserRFC4180<T>(options, reader)
            : new CsvParserUnix<T>(options, reader);
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
        ArgumentNullException.ThrowIfNull(options);

        var reader = CsvPipeReader.Create(in csv);

        options.MakeReadOnly();
        return !options.Dialect.Escape.HasValue
            ? new CsvParserRFC4180<T>(options, reader)
            : new CsvParserUnix<T>(options, reader);
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
