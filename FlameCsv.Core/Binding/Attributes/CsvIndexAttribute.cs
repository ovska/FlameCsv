using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Binds the property, field or constructor parameter to CSV field at <see cref="Index"/>.
/// </summary>
/// <remarks>
/// Field indexes start at zero.
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvIndexAttribute : Attribute, ICsvBindingAttribute
{
    /// <summary>CSV field index.</summary>
    public int Index { get; }

    /// <inheritdoc cref="ICsvBindingAttribute.Scope"/>
    public CsvBindingScope Scope { get; set; }

    /// <inheritdoc cref="CsvIndexAttribute"/>
    /// <param name="index">CSV field index</param>
    public CsvIndexAttribute(int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Index = index;
    }
}
