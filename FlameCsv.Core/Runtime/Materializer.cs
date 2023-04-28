using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Parsers;
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
    /// <param name="parser">Parser instance</param>
    /// <typeparam name="TValue">Parsed value</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // should be small enough to inline in Parse()
    protected TValue ParseNext<TValue>(ref CsvEnumerationStateRef<T> state, ICsvParser<T, TValue> parser)
    {
        if (!state.remaining.IsEmpty)
        {
            ReadOnlyMemory<T> field = !state._context.Dialect.Escape.HasValue
                ? RFC4180Mode<T>.ReadNextField(ref state)
                : EscapeMode<T>.ReadNextField(ref state);

            if (parser.TryParse(field.Span, out TValue? value))
                return value;

            ThrowParseFailed(ref state, field, parser);
        }

        ThrowRecordEndedPrematurely(ref state);
        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)] // should be small enough to inline in Parse()
    protected static TValue ParseKnown<TValue>(ReadOnlySpan<T> field, ICsvParser<T , TValue> parser)
    {
        if (parser.TryParse(field, out TValue? value))
            return value;

        ThrowParseFailed();
        return default!;
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowRecordEndedPrematurely(ref CsvEnumerationStateRef<T> state)
    {
        state.ThrowRecordEndedPrematurely(FieldCount, RecordType);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowParseFailed(ref CsvEnumerationStateRef<T> state, ReadOnlyMemory<T> field, ICsvParser<T> parser)
    {
        state.ThrowParseFailed(field.Span, parser);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowParseFailed()
    {
        throw new CsvParseException();
    }

    public override string ToString()
    {
        return $"Materializer<{string.Join(
            ", ",
            GetType().GetGenericArguments().Select(t => t.ToTypeString().Replace(t.Namespace + '.', "")))}>";
    }
}
