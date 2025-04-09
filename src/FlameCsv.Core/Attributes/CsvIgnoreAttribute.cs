﻿using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Ignores the member or parameter when reading or writing CSV.<br/>
/// Has no effect for parameters without a default value, or on <c>init</c> only properties.<br/>
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
public sealed class CsvIgnoreAttribute : CsvFieldConfigurationAttribute;
