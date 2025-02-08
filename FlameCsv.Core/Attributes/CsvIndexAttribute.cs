using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures the field index used when reading or writing CSV.<br/>
/// When not placed on a member or parameter, <see cref="CsvFieldConfigurableBaseAttribute.MemberName"/> must be set.<br/>
/// When placed on an assembly, <see cref="CsvTypeConfigurableBaseAttribute.TargetType"/> must be set.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter |
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Assembly,
    AllowMultiple = true)]
[PublicAPI]
public sealed class CsvIndexAttribute : CsvFieldConfigurableBaseAttribute
{
    /// <summary>
    /// 0-based index of the field when reading or writing headerless CSV.<br/>
    /// Indexes must be unique within a type.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvIndexAttribute"/> class.
    /// </summary>
    /// <param name="index">0-based index of the field when reading or writing headerless CSV.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public CsvIndexAttribute(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        Index = index;
    }
}
