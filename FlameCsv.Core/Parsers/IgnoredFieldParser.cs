using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Parsers;

// Keep the parsed type in sync with CsvBinding.Type for ignored bindings.
internal sealed class IgnoredFieldParser<T> : ICsvParser<T, object?> where T : unmanaged, IEquatable<T>
{
    public static IgnoredFieldParser<T> Instance { get; } = new IgnoredFieldParser<T>();

    public bool CanParse(Type resultType) => true;

    public bool TryParse(ReadOnlySpan<T> span, [MaybeNullWhen(false)] out object? value)
    {
        value = default;
        return true;
    }
}
