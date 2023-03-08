using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Marks the column at index as ignored.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class CsvIndexIgnoreAttribute : Attribute
{
    /// <summary>CSV column index.</summary>
    public int Index { get; }

    /// <inheritdoc cref="CsvIndexIgnoreAttribute"/>
    /// <param name="index">CSV column index</param>
    public CsvIndexIgnoreAttribute(int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Index = index;
    }
}
