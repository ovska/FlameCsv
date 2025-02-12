using System.Diagnostics.CodeAnalysis;

namespace FlameCsv;

/// <summary>
/// Parses and formats <typeparamref name="TValue"/> to/from CSV fields.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Parsed/formatted value</typeparam>
public abstract class CsvConverter<T, TValue> : CsvConverter<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns whether the type can be handled by this converter.
    /// </summary>
    /// <param name="type">Type to check</param>
    /// <returns><see langword="true"/> if the converter is suitable for <paramref name="type"/></returns>
    public override bool CanConvert(Type type) => type == typeof(TValue);

    /// <summary>
    /// Attempts to parse <paramref name="value"/> from the field.
    /// </summary>
    /// <param name="source">CSV field</param>
    /// <param name="value">Parsed value</param>
    /// <returns><see langword="true"/> if the value was successfully parsed.</returns>
    public abstract bool TryParse(ReadOnlySpan<T> source, [MaybeNullWhen(false)] out TValue value);

    /// <summary>
    /// Attempts to format <paramref name="value"/> into the field.
    /// </summary>
    /// <param name="destination">Buffer to format the value to</param>
    /// <param name="value">Value to format</param>
    /// <param name="charsWritten">If successful, how many characters were written to <paramref name="destination"/></param>
    /// <returns><see langword="true"/> if the value was successfully formatted.</returns>
    public abstract bool TryFormat(Span<T> destination, TValue value, out int charsWritten);

    /// <summary>
    /// Whether the converter formats null values. When <see langword="false"/> (the default),
    /// <see cref="CsvOptions{T}.GetNullToken(Type)"/> is used to write nulls.
    /// </summary>
    protected internal virtual bool CanFormatNull => false;
}
