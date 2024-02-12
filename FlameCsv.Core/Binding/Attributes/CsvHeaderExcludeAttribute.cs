namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Excludes the property from being matched from CSV header.
/// </summary>
public sealed class CsvHeaderExcludeAttribute : CsvHeaderConfigurationAttribute, ICsvBindingAttribute
{
    /// <inheritdoc cref="CsvHeaderExcludeAttribute"/>
    public CsvHeaderExcludeAttribute()
    {
    }

    public CsvBindingScope Scope => CsvBindingScope.Read;
}
