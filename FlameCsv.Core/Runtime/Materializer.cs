using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Reading;

namespace FlameCsv.Runtime;

/// <summary>
/// State of a CSV row that is being parsed.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
internal abstract partial class Materializer<T> where T : unmanaged, IEquatable<T>
{
    /// <inheritdoc cref="IMaterializer{T, TResult}.FieldCount" />
    public abstract int FieldCount { get; }

    /// <summary>
    /// Parses the next field from the <paramref name="reader"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // should be small enough to inline in Parse()
    protected TValue ParseNext<TValue, TReader>(
        ref TReader reader,
        CsvConverter<T, TValue> converter)
        where TReader : ICsvFieldReader<T>, allows ref struct
    {
        if (reader.MoveNext())
        {
            ReadOnlySpan<T> field = reader.Current;

            if (converter.TryParse(field, out TValue? value))
                return value;

            CsvParseException.Throw(reader.Options, field, converter);
        }

        CsvReadException.ThrowForPrematureEOF(FieldCount, reader.Options, reader.Record);
        return default;
    }

    public override string ToString() => GetType().FullName ?? GetType().Name;
}
