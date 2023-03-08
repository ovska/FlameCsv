using System.Collections.Immutable;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Defines the value(s) for header name matching to use instead of member name in built-in header binding providers.
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
    public ImmutableArray<string> Values { get; }

    /// <summary>
    /// Determines the order in which the members are checked. Default is 1, which checks the attributes
    /// before undecorated members, which have an order of 0.
    /// </summary>
    public int Order { get; set; } = 1;

    /// <inheritdoc cref="CsvHeaderAttribute"/>
    public CsvHeaderAttribute(string value)
    {
        Values = ImmutableArray.Create(value);
    }

    /// <inheritdoc cref="CsvHeaderAttribute"/>
    public CsvHeaderAttribute(params string[] values)
    {
        Guard.IsNotNull(values);
        Guard.IsNotEmpty(values);
        Values = ImmutableArray.Create(values);
    }
}
