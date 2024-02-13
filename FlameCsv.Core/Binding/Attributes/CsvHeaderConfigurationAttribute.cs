namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Base class for <see cref="CsvHeaderAttribute"/> and <see cref="CsvHeaderExcludeAttribute"/> to
/// ensure only one is used.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public abstract class CsvHeaderConfigurationAttribute : Attribute, ICsvBindingAttribute
{
    /// <inheritdoc cref="ICsvBindingAttribute.Scope"/>
    public CsvBindingScope Scope { get; set; }
}
