using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Converters;

// Keep the type in sync with CsvBinding.Type for ignored bindings.
internal sealed class IgnoredConverter<T> : CsvConverter<T, object?> where T : unmanaged, IEquatable<T>
{
    protected internal override bool HandleNull => true;

    public static IgnoredConverter<T> Instance { get; } = new IgnoredConverter<T>();

    public override bool TryFormat(Span<T> destination, object? value, out int charsWritten)
    {
        charsWritten = 0;
        return true;
    }

    public override bool TryParse(ReadOnlySpan<T> source, [MaybeNullWhen(false)] out object? value)
    {
        value = null;
        return true;
    }
}
