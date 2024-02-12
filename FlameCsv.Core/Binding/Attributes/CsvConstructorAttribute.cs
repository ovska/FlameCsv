namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Forces the decorated constructor to be used when creating the type while reading CSV.
/// </summary>
/// <remarks>
/// If omitted, the parameterless constructor is used.
/// </remarks>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class CsvConstructorAttribute : Attribute, ICsvBindingAttribute
{
    public CsvBindingScope Scope => CsvBindingScope.Read;
}
