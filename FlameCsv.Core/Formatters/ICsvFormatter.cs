using FlameCsv.Writers;

namespace FlameCsv.Formatters;

/// <summary>
/// Base interface for formatting types to CSV fields.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvFormatter<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns whether the type can be handled by this formatter, or a suitable formatter can be
    /// created if this is a factory instance.
    /// </summary>
    /// <remarks>
    /// For simple parsers this check is usually just whether the parameter is the type being parsed in the
    /// <see cref="ICsvFormatter{T,TValue}"/>.
    /// </remarks>
    /// <param name="valueType">Type to check</param>
    /// <returns><see langword="true"/> if the type can be parsed</returns>
    bool CanFormat(Type valueType);

    /// <inheritdoc cref="CanFormat(Type)"/>
    bool CanFormat<TValue>() => CanFormat(typeof(TValue));

    /// <summary>
    /// Returns whether the formatter can handle null values. The default is <see langword="false"/>,
    /// in which case a null token for the type is retrieved from the options via <see cref="CsvWriterOptions{T}.GetNullToken(Type)"/>.
    /// </summary>
    /// <remarks>
    /// For value types other than <see cref="Nullable{T}"/>, this property is not used.
    /// </remarks>
    bool HandleNull => false;
}

/// <summary>
/// Formatter instance for <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Formatted value</typeparam>
public interface ICsvFormatter<T, in TValue> : ICsvFormatter<T>
    where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Attempts to format <paramref name="value"/> to the destination buffer.
    /// </summary>
    /// <param name="value">Value to format</param>
    /// <param name="destination">Destination buffer to write the value to</param>
    /// <param name="tokensWritten">How many tokens were written</param>
    /// <returns>
    /// <see langword="true"/> if the value was successfully formatted,
    /// false if there wasn't enough space in the destination buffer.
    /// </returns>
    bool TryFormat(TValue value, Span<T> destination, out int tokensWritten);
}
