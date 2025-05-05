using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Marks the member or parameter as required when reading CSV.
/// <c>required</c> or <c>init</c> properties, and parameters without a default value are implicitly required.<br/>
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
    AllowMultiple = true)] // type and assembly need AllMultiple = true
[PublicAPI]
public sealed class CsvRequiredAttribute : CsvFieldConfigurationAttribute;
