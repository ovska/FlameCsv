namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Excludes the property from being matched from CSV header.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class CsvHeaderIgnoreAttribute : Attribute
{
    /// <inheritdoc cref="CsvHeaderIgnoreAttribute"/>
    public CsvHeaderIgnoreAttribute()
    {
    }
}
