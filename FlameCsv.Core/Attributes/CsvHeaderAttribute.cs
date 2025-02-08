using System.Collections.Immutable;
using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures the header value(s) used when reading or writing CSV.<br/>
/// When not placed on a member or parameter, <see cref="CsvFieldConfigurationAttribute.MemberName"/> must be set.<br/>
/// When placed on an assembly, <see cref="CsvConfigurationAttribute.TargetType"/> must be set.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter |
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Interface |
    AttributeTargets.Assembly,
    AllowMultiple = true)]
[PublicAPI]
public sealed class CsvHeaderAttribute : CsvFieldConfigurationAttribute
{
    /// <summary>
    /// Header value used when reading or writing CSV.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Additional values that can be used to match the header when reading CSV.
    /// <see cref="Value"/> is always used when writing.
    /// </summary>
    public ImmutableArray<string> AdditionalValues { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvHeaderAttribute"/> class.
    /// </summary>
    /// <param name="value">Header value used when reading or writing CSV.</param>
    /// <param name="additionalValues">
    /// Additional values that can be used to match the header when reading CSV.
    /// </param>
    public CsvHeaderAttribute(string value, params string[] additionalValues)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(additionalValues);

        for (int i = 0; i < additionalValues.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(additionalValues[i]);
        }

        Value = value;
        AdditionalValues = [..additionalValues];
    }
}
