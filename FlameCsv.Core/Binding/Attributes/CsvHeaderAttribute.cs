using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Defines the value(s) for header name matching to use instead of member name in built-in header binding providers.
/// To just affect the order, leave the values empty to default to member name.
/// </summary>
/// <remarks>
/// The provider determines case sensitivity, <see cref="HeaderTextBinder"/> or <see cref="HeaderUtf8Binder"/>
/// for the default implementations.
/// </remarks>
public sealed class CsvHeaderAttribute : CsvHeaderConfigurationAttribute
{
    /// <summary>
    /// Value(s) to match to header.
    /// </summary>
    public string[] Values { get; }

    /// <summary>
    /// Determines the order in which the members are checked. Default is 1, which checks the attributes
    /// before undecorated members, which have an order of 0.
    /// </summary>
    public int Order { get; set; } = 1;

    /// <summary>
    /// Whether the member must be matched. Default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Constructor parameters without a default value are always required.
    /// </remarks>
    public bool Required { get; set; }

    /// <inheritdoc cref="CsvHeaderAttribute"/>
    /// <param name="values">
    /// Values to match against the header. Leave empty to configure only order and whether the member is required.
    /// </param>
    public CsvHeaderAttribute(params string[] values)
    {
        Guard.IsNotNull(values);
        Values = values;
    }
}
