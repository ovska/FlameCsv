using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Marks the column at indexes as ignored.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class CsvIndexIgnoreAttribute : Attribute
{
    /// <summary>Ignored column indexes.</summary>
    public ReadOnlySpan<int> Indexes => _indexes;

    private readonly int[] _indexes;

    /// <inheritdoc cref="CsvIndexIgnoreAttribute"/>
    /// <param name="index">CSV column index</param>
    public CsvIndexIgnoreAttribute(params int[] indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);

        foreach (var index in indexes)
            Guard.IsGreaterThanOrEqualTo(index, 0);

        _indexes = indexes;
    }
}
