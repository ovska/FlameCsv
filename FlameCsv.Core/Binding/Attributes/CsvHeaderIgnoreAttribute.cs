namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Excludes the property from being matched from CSV header.
/// </summary>
[AttributeUsage(CsvBinding.AllowedOn)]
public sealed class CsvHeaderIgnoreAttribute : Attribute
{
    /// <inheritdoc cref="CsvHeaderIgnoreAttribute"/>
    public CsvHeaderIgnoreAttribute()
    {
    }
}
