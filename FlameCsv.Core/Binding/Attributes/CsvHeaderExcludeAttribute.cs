namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Excludes the property from being matched from CSV header.
/// </summary>
public sealed class CsvHeaderExcludeAttribute : CsvHeaderConfigurationAttribute
{
    /// <inheritdoc cref="CsvHeaderExcludeAttribute"/>
    public CsvHeaderExcludeAttribute()
    {
    }
}
