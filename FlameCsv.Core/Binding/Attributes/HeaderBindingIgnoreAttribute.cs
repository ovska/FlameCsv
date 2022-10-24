namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Excludes the property from header name matching in built-in header binding providers.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class HeaderBindingIgnoreAttribute : Attribute
{
    /// <inheritdoc cref="HeaderBindingIgnoreAttribute"/>
    public HeaderBindingIgnoreAttribute()
    {
    }
}
