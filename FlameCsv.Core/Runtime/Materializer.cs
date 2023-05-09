using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
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
    protected TValue ParseNext<TReader, TValue>(ref TReader reader, ICsvParser<T, TValue> parser)
        where TReader : ICsvFieldReader<T>
    {
        if (reader.TryReadNext(out ReadOnlyMemory<T> field))
        {
            if (parser.TryParse(field.Span, out TValue? value))
                return value;

            ThrowParseFailed(field, parser);
        }

        ThrowRecordEndedPrematurely();
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
    private void ThrowRecordEndedPrematurely(/*ref CsvEnumerationStateRef<T> state*/)
    {
        throw new NotImplementedException();
        //state.ThrowRecordEndedPrematurely(FieldCount, RecordType);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowParseFailed(/*ref CsvEnumerationStateRef<T> state, */ReadOnlyMemory<T> field, ICsvParser<T> parser)
    {
        throw new NotImplementedException();
        //state.ThrowParseFailed(field.Span, parser);
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
