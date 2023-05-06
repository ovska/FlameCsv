namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Marks the member as being required when matching to header fields.
/// </summary>
/// <remarks>
/// Constructor parameters are always required, and this attribute not usable on them.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CsvHeaderRequiredAttribute : Attribute
{
}
