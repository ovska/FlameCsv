using System.Diagnostics;

namespace FlameCsv.Reading;

/// <summary>
/// Represents a CSV record spanning one line, before the individual fields are read.
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerDisplay(@"\{ CsvLine Length: {Value.Length}, QuoteCount: {QuoteCount}, EscapeCount: {EscapeCount} \}")]
public readonly struct CsvLine<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Raw value of the line.
    /// </summary>
    public required ReadOnlyMemory<T> Value { get; init; }

    /// <summary>
    /// How many quotes there were on the line.
    /// </summary>
    public required uint QuoteCount { get; init; }

    /// <summary>
    /// How many escape tokens there were on the line.
    /// </summary>
    /// <remarks>
    /// Always zero if <see cref="CsvOptions{T}.Escape"/> is null.
    /// </remarks>
    public uint EscapeCount { get; init; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
