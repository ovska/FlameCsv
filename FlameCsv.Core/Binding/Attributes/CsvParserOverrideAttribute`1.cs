using FlameCsv.Parsers;

namespace FlameCsv.Binding.Attributes;

/// <inheritdoc/>
public sealed class CsvParserOverrideAttribute<T, TParser> : CsvParserOverrideAttribute
    where T : unmanaged, IEquatable<T>
    where TParser : ICsvParser<T>
{
    /// <inheritdoc/>
    public CsvParserOverrideAttribute() : base(typeof(TParser))
    {
    }
}
