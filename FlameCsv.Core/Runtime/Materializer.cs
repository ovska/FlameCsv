using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
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
    protected static TValue ParseNext<TReader, TValue>(ref TReader reader, CsvConverter<T, TValue> converter)
        where TReader : struct, ICsvFieldReader<T>
    {
        if (reader.TryReadNext(out ReadOnlyMemory<T> field))
        {
            if (converter.TryParse(field.Span, out TValue? value))
                return value;

            reader.ThrowParseFailed(field, converter);
        }

        reader.ThrowForInvalidEOF();
        return default;
    }

    public override string ToString()
    {
        return $"Materializer<{string.Join(
            ", ",
            GetType().GetGenericArguments().Select(t => t.ToTypeString().Replace(t.Namespace + '.', "")))}>";
    }
}
