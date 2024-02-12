namespace FlameCsv.Binding.Attributes;

public interface ICsvBindingAttribute
{
    /// <summary>
    /// Whether the attribute is only valid for reading.
    /// </summary>
    CsvBindingScope Scope { get; }
}
