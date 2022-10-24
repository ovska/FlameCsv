using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Binds the property or field to CSV column at <see cref="Index"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class IndexBindingAttribute : Attribute
{
    /// <summary>CSV column index.</summary>
    public int Index { get; }

    /// <inheritdoc cref="IndexBindingAttribute"/>
    /// <param name="index">CSV column index</param>
    public IndexBindingAttribute(int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Index = index;
    }
}
