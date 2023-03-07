namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Excludes the property from being matched from CSV header.
/// </summary>
[AttributeUsage(CsvBinding.AllowedOn)]
public sealed class CsvHeaderExcludeAttribute : Attribute
{
    /// <inheritdoc cref="CsvHeaderExcludeAttribute"/>
    public CsvHeaderExcludeAttribute()
    {
    }
}
