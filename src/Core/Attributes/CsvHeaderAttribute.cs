using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures the header name used when reading or writing CSV.<br/>
/// When not placed on a member or parameter, <see cref="CsvFieldConfigurationAttribute.MemberName"/> must be set.<br/>
/// When placed on an assembly, <see cref="CsvConfigurationAttribute.TargetType"/> must be set.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property
        | AttributeTargets.Field
        | AttributeTargets.Parameter
        | AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Interface
        | AttributeTargets.Assembly,
    AllowMultiple = true
)] // type and assembly need AllMultiple = true
[PublicAPI]
public sealed class CsvHeaderAttribute : CsvFieldConfigurationAttribute
{
    /// <summary>
    /// Header value used when reading or writing CSV.
    /// If <c>null</c>, the member or parameter name is used as the header value (the default).
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Additional values that can be used to match the header when reading CSV.
    /// <see cref="Value"/> is always used when writing.
    /// </summary>
    public string[] Aliases
    {
        get => field;
        init
        {
            ValidateAliases(value);
            field = value;
        }
    } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvHeaderAttribute"/> class.
    /// </summary>
    /// <param name="value">Header value used when reading or writing CSV.</param>
    /// <param name="aliases">Additional values that can be used to match the header when reading CSV.</param>
    public CsvHeaderAttribute(string? value, params string[] aliases)
    {
        ValidateAliases(aliases);
        Value = value;
        Aliases = aliases;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvHeaderAttribute"/> class.
    /// </summary>
    /// <param name="value">Header value used when reading or writing CSV.</param>
    public CsvHeaderAttribute(string? value)
    {
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvHeaderAttribute"/> class.
    /// </summary>
    public CsvHeaderAttribute() { }

    private static void ValidateAliases(string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        for (int i = 0; i < aliases.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(aliases[i]);
        }
    }
}
