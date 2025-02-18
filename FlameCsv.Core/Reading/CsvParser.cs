using System.Runtime.CompilerServices;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Provides a static factory method for creating <see cref="CsvParser{T}"/> instances.
/// </summary>
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
    [MustDisposeResource]
    public static CsvParser<T> Create<T>(CsvOptions<T> options) where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();
        return !options.Dialect.Escape.HasValue
            ? new CsvParserRFC4180<T>(options)
            : new CsvParserUnix<T>(options);
    }

    private protected CsvParser()
    {
    }
}
