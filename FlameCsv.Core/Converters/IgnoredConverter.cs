﻿using FlameCsv.Binding;

namespace FlameCsv.Converters;

// Keep the type in sync with CsvBinding.Type for ignored bindings.
internal sealed class IgnoredConverter<T> : CsvConverter<T, CsvIgnored> where T : unmanaged, IBinaryInteger<T>
{
    protected internal override bool CanFormatNull => true;

    public static IgnoredConverter<T> Instance { get; } = new();

    public override bool TryFormat(Span<T> destination, CsvIgnored value, out int charsWritten)
    {
        charsWritten = 0;
        return true;
    }

    public override bool TryParse(ReadOnlySpan<T> source, out CsvIgnored value)
    {
        value = default;
        return true;
    }
}
