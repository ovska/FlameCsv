using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
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

    /// <summary>The return type for a CSV record.</summary>
    protected abstract Type RecordType { get; }

    /// <summary>
    /// Parses the next field from the <paramref name="enumerator"/>.
    /// </summary>
    /// <param name="converter">Converter instance</param>
    /// <typeparam name="TValue">Parsed value</typeparam>
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

        CsvReadException.ThrowForPrematureEOF(FieldCount, reader.Options, reader.RawRecord);
        return default;
    }

    public override string ToString() => GetType().ToTypeString();

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected static void ThrowNotFullyConsumed<TReader>(int fieldCount, ref readonly TReader reader)
        where TReader : ICsvFieldReader<T>, allows ref struct
    {
        throw new CsvFormatException(
            $"Csv record was expected to have {fieldCount} fields, but had more: " +
            reader.Options.AsPrintableString(reader.RawRecord));
    }
}
