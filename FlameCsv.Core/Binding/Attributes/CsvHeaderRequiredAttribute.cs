namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Marks the member as being required when matching to header fields.
/// </summary>
/// <remarks>
/// Constructor parameters without default values are always required.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CsvHeaderRequiredAttribute : Attribute
{
}
