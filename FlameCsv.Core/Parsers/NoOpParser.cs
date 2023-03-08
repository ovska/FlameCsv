using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Parsers;

// Keep the parsed type in sync with CsvBinding.Type for ignored bindings.
internal sealed class NoOpParser<T> : ICsvParser<T, object?> where T : unmanaged, IEquatable<T>
{
    public static NoOpParser<T> Instance { get; } = new NoOpParser<T>();

    public bool CanParse(Type resultType) => true;

    public bool TryParse(ReadOnlySpan<T> span, [MaybeNullWhen(false)] out object? value)
    {
        value = default;
        return true;
    }
}
