namespace FlameCsv.Binding;

/// <summary>
/// Sentinel type for ignored column when reading/writing CSV.
/// </summary>
/// <remarks>
/// For example, when reading/writing tuples or value-tuples you can use this type
/// in one position to ignore the field in that index when reading, or always write an empty
/// value when writing.
/// </remarks>
public readonly struct CsvIgnored;
