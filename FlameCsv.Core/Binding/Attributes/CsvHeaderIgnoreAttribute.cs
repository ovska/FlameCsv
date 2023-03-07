namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Always ignores the specified columns when CSV has a header record.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class CsvHeaderIgnoreAttribute : Attribute
{
    public StringComparison Comparison { get; set; } = StringComparison.OrdinalIgnoreCase;

    public ReadOnlyMemory<string> Values { get; }

    /// <inheritdoc cref="CsvHeaderExcludeAttribute"/>
    public CsvHeaderIgnoreAttribute(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = values;
    }
}
