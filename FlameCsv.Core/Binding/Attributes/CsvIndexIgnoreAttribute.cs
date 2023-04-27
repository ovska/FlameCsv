using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Marks the field at indexes as ignored.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class CsvIndexIgnoreAttribute : Attribute
{
    /// <summary>Ignored field indexes.</summary>
    public ReadOnlySpan<int> Indexes => _indexes;

    private readonly int[] _indexes;

    /// <inheritdoc cref="CsvIndexIgnoreAttribute"/>
    /// <param name="indexes">CSV field indexes</param>
    public CsvIndexIgnoreAttribute(params int[] indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);

        foreach (var index in indexes)
        {
            if (index < 0)
                ThrowHelper.ThrowArgumentException(nameof(indexes), "All indexes must be non-negative.");
        }

        _indexes = indexes;
    }
}
