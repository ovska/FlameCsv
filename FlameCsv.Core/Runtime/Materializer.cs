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
    /// Parses the next column from the <paramref name="enumerator"/>.
    /// </summary>
    /// <param name="parser">Parser instance</param>
    /// <typeparam name="TValue">Parsed value</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // should be small enough to inline in Parse()
    protected TValue ParseNext<TValue>(ref CsvEnumerationStateRef<T> state, ICsvParser<T, TValue> parser)
    {
        if (RFC4180Mode<T>.TryGetField(ref state, out ReadOnlyMemory<T> field))
        {
            if (parser.TryParse(field.Span, out TValue? value))
                return value;

            ThrowParseFailed(ref state, field, parser);
        }

        ThrowMoveNextFailed(ref state);
        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void EnsureAllFieldsRead(ref CsvEnumerationStateRef<T> state)
    {
        if (!state.remaining.IsEmpty)
        {
            ThrowNotAllFieldsRead(ref state);
        }
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowMoveNextFailed(ref CsvEnumerationStateRef<T> state)
    {
        throw new CsvFormatException(
            $"Expected the record to have {FieldCount} fields when parsing {RecordType}, but it ended prematurely. " +
            $"Record: {state.Record.Span.AsPrintableString(state.ExposeContent, state.Dialect)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowNotAllFieldsRead(ref CsvEnumerationStateRef<T> state)
    {
        throw new CsvFormatException(
            $"Expected the record to have exactly {FieldCount} fields, but the record had leftover data. " +
            $"Record: {state.Record.Span.AsPrintableString(state.ExposeContent, state.Dialect)}, " +
            $"Leftover data: {state.remaining.Span.AsPrintableString(state.ExposeContent, state.Dialect)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowParseFailed(
        ref CsvEnumerationStateRef<T> state,
        ReadOnlyMemory<T> field,
        ICsvParser<T> parser)
    {
        throw new CsvParseException(
            $"Failed to parse with {parser.GetType()} from {field.Span.AsPrintableString(state.ExposeContent, state.Dialect)} "
            + $"in {GetType().ToTypeString()}.")
        { Parser = parser };
    }
}
