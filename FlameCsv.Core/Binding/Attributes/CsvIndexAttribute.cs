using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Binds the property, field or constructor parameter to CSV column at <see cref="Index"/>.
/// </summary>
/// <remarks>
/// Column indexes start at zero.
/// </remarks>
[AttributeUsage(CsvBinding.AllowedOn)]
public sealed class CsvIndexAttribute : Attribute
{
    /// <summary>CSV column index.</summary>
    public int Index { get; }

    /// <inheritdoc cref="CsvIndexAttribute"/>
    /// <param name="index">CSV column index</param>
    public CsvIndexAttribute(int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Index = index;
    }
}
