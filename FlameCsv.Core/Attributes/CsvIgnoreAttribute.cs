using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Ignores the member or property when reading or writing CSV.<br/>
/// When not placed on a member or parameter, <see cref="CsvFieldConfigurableBaseAttribute.MemberName"/> must be set.<br/>
/// When placed on an assembly, <see cref="CsvTypeConfigurableBaseAttribute.TargetType"/> must be set.
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
public sealed class CsvIgnoreAttribute : CsvFieldConfigurableBaseAttribute;
