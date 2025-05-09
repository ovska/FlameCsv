namespace FlameCsv.Benchmark.Comparisons;

internal sealed class FloatUtf8Parser : CsvConverter<byte, float>
{
    public override bool TryParse(ReadOnlySpan<byte> source, out float value)
    {
        return csFastFloat.FastFloatParser.TryParseFloat(source, out value);
    }

    public override bool TryFormat(Span<byte> destination, float value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten);
    }
}
