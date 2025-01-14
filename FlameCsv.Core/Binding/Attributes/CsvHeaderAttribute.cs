namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Defines the value(s) for header name matching to use instead of member name in built-in header binding providers.
/// To just affect the order, leave the values empty to default to member name.
/// </summary>
/// <remarks>
/// <see cref="CsvOptions{T}.Comparer"/> is used for comparisons.
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
    /// Required init-only properties and constructor parameters without a default value are always required.
    /// </remarks>
    public bool Required { get; set; }

    /// <inheritdoc cref="CsvHeaderAttribute"/>
    /// <param name="values">
    /// Values to match against the header. When empty, member/parameter name is used.
    /// </param>
    public CsvHeaderAttribute(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        for (int i = 0; i < values.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(values[i]);
        }

        Values = values;
    }
}
