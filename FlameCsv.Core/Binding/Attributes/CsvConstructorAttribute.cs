namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Forces the decorated constructor to be used when creating the type while reading CSV.
/// Unnecessary for parameterless constructors.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class CsvConstructorAttribute : Attribute
{
}
