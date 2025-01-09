using System.Diagnostics;

namespace FlameCsv.Reading;

[DebuggerDisplay(@"\{ CsvLine Length: {Value.Length}, QuoteCount: {QuoteCount}, EscapeCount: {EscapeCount} \}")]
public readonly struct CsvLine<T> where T : unmanaged, IBinaryInteger<T>
{
    public required ReadOnlyMemory<T> Value { get; init; }
    public required uint QuoteCount { get; init; }
    public uint EscapeCount { get; init; }

    public override string ToString() => Value.ToString();
}
