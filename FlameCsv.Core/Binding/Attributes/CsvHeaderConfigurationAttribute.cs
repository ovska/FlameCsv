namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Base class for <see cref="CsvHeaderAttribute"/> and <see cref="CsvHeaderExcludeAttribute"/> to
/// ensure only one is used.
/// </summary>
[AttributeUsage(CsvBinding.AllowedOn, AllowMultiple = false)]
public abstract class CsvHeaderConfigurationAttribute : Attribute
{

}
