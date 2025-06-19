namespace FlameCsv.Benchmark.Comparisons;

internal sealed class DoubleUtf8Parser : CsvConverter<byte, double>
{
    public override bool TryParse(ReadOnlySpan<byte> source, out double value)
    {
        return csFastFloat.FastDoubleParser.TryParseDouble(source, out value);
    }

    public override bool TryFormat(Span<byte> destination, double value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten);
    }
}
