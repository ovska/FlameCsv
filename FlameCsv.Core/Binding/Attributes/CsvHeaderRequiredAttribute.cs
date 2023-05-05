namespace FlameCsv.Binding.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class CsvHeaderRequiredAttribute : Attribute
{
}
