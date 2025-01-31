using JetBrains.Annotations;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Configures the type for reading and writing CSV.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class CsvTypeAttribute : Attribute
{
    /// <summary>
    /// Header values to always ignore when reading CSV.
    /// </summary>
    public string[]? IgnoredHeaders { get; init; }

    /// <summary>
    /// Indexes to always ignore when reading CSV.
    /// </summary>
    public int[]? IgnoredIndexes { get; init; }
}
