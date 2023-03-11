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
internal abstract partial class Materializer
{
    /// <inheritdoc cref="IMaterializer{T,TResult}.ColumnCount"/>
    public abstract int ColumnCount { get; }

    /// <summary>
    /// Parses the next column from the <paramref name="enumerator"/> to <paramref name="value"/>.
    /// </summary>
    /// <param name="enumerator">Column enumerator</param>
    /// <param name="parser">Parser instance</param>
    /// <param name="value">Target to assign the parsed value to</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <typeparam name="TValue">Parsed value</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // should be small enough to inline in Parse()
    protected void ParseNext<T, TValue>(
        ref CsvColumnEnumerator<T> enumerator,
        ICsvParser<T, TValue> parser,
        out TValue value)
        where T : unmanaged, IEquatable<T>
    {
        Unsafe.SkipInit(out value); // reference types are always zeroed by jit

        if (enumerator.MoveNext())
        {
            if (parser.TryParse(enumerator.Current, out value!))
                return;

            ThrowParseFailed(ref enumerator, parser);
        }

        ThrowMoveNextFailed(ref enumerator);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowMoveNextFailed<T>(ref CsvColumnEnumerator<T> enumerator)
        where T : unmanaged, IEquatable<T>
    {
        throw new CsvFormatException(
            $"Got only {enumerator.Column} columns out of {ColumnCount} in {GetType().ToTypeString()}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowParseFailed<T>(
        ref CsvColumnEnumerator<T> enumerator,
        ICsvParser<T> parser)
        where T : unmanaged, IEquatable<T>
    {
        throw new CsvParseException(
            $"Failed to parse with {parser.GetType()} from {enumerator.Current.ToString()} "
            + $"in {GetType().ToTypeString()}")
            {
                Parser = parser,
            };
    }
}
