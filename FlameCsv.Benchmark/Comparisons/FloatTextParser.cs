﻿namespace FlameCsv.Benchmark.Comparisons;

internal sealed class FloatTextParser : CsvConverter<char, float>
{
    public override bool TryParse(ReadOnlySpan<char> source, out float value)
    {
        return csFastFloat.FastFloatParser.TryParseFloat(source, out value);
    }

    public override bool TryFormat(Span<char> destination, float value, out int charsWritten)
    {
        return value.TryFormat(destination, out charsWritten);
    }
}
