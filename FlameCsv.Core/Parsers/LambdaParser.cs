using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Parsers;

/// <summary>
/// Parser that parses <typeparam name="TValue"/> using the supplied function.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Value to parse</typeparam>
public class LambdaParser<T, TValue> : ICsvParser<T, TValue> where T : unmanaged, IEquatable<T>
{
    protected readonly CsvTryParse<T, TValue> _tryParse;

    public LambdaParser(CsvTryParse<T, TValue> tryParse)
    {
        ArgumentNullException.ThrowIfNull(tryParse);
        _tryParse = tryParse;
    }

    public virtual bool TryParse(ReadOnlySpan<T> span, [MaybeNullWhen(false)] out TValue value) => _tryParse(span, out value);
    public virtual bool CanParse(Type resultType) => resultType == typeof(TValue);
}
