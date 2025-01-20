namespace FlameCsv.Binding.Attributes;

// ReSharper disable UnusedMemberInSuper.Global
internal interface ICsvBindingAttribute
{
    /// <summary>
    /// Determines whether the attribute is valid for reading CSV, writing CSV, or both (the default).
    /// </summary>
    CsvBindingScope Scope { get; }
}
