using JetBrains.Annotations;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Configures the type for reading and writing CSV.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public class CsvTypeAttribute : Attribute
{
    /// <summary>
    /// Header values to always ignore when reading CSV.
    /// </summary>
    public string[]? IgnoredHeaders { get; init; }

    /// <summary>
    /// Indexes to always ignore when reading CSV.
    /// </summary>
    public int[]? IgnoredIndexes { get; init; }

    /// <summary>
    /// Type that will be created in place of the target type.
    /// </summary>
    /// <remarks>
    /// Intended to be used for reading interfaces and abstract classes.<br/>
    /// Has no effect when writing.
    /// </remarks>
    [DAM(Messages.ReflectionBound)]
    public Type? CreatedTypeProxy { get; init; }
}
