namespace FlameCsv.Binding.Attributes;

internal interface ICsvBindingAttribute
{
    /// <summary>
    /// Whether the attribute is only valid for reading.
    /// </summary>
    CsvBindingScope Scope { get; }
}
