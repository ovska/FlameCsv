using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Parsers;

/// <summary>
/// Base interface for parsing CSV columns.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvParser<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns whether <paramref name="resultType"/> can be handled by this parser, or a suitable parser can be
    /// created if this is a factory instance.
    /// </summary>
    /// <remarks>
    /// For simple parsers this check is usually just whether the parameter is the type being parsed in the
    /// <see cref="ICsvParser{T,TValue}"/>.
    /// </remarks>
    /// <param name="resultType">Type to check</param>
    /// <returns><see langword="true"/> if the type can be parsed</returns>
    bool CanParse(Type resultType);
}

/// <summary>
/// Parser instance for <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Parsed value</typeparam>
public interface ICsvParser<T, TValue> : ICsvParser<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Attempts to parse <paramref name="value"/> from the column.
    /// </summary>
    /// <param name="span">Column tokens</param>
    /// <param name="value">Parsed value</param>
    /// <returns><see langword="true"/> if the value was successfully parsed.</returns>
    bool TryParse(ReadOnlySpan<T> span, [MaybeNullWhen(false)] out TValue value);
}
