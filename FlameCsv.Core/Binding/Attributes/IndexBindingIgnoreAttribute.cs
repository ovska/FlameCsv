using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Marks the column at index as ignored.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class IndexBindingIgnoreAttribute : Attribute
{
    /// <summary>CSV column index.</summary>
    public int Index { get; }

    /// <inheritdoc cref="IndexBindingIgnoreAttribute"/>
    /// <param name="index">CSV column index</param>
    public IndexBindingIgnoreAttribute(
        int index)
    {
        Guard.IsGreaterThanOrEqualTo(index, 0);
        Index = index;
    }
}
