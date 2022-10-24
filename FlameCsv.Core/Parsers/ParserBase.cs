using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Parsers;

/// <summary>
/// Utility base class for simple parsers where <see cref="CanParse"/> returns whether the parameter type is
/// <typeparamref name="TValue"/>
/// </summary>
public abstract class ParserBase<T, TValue> : ICsvParser<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    /// <inheritdoc/>
    public abstract bool TryParse(ReadOnlySpan<T> span, [MaybeNullWhen(false)] out TValue value);

    /// <inheritdoc/>
    public virtual bool CanParse(Type resultType) => resultType == typeof(TValue);
}
