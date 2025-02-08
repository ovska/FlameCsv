using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Configures the order in which fields will be written when writing CSV (and headers matched when reading).<br/>
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
public sealed class CsvOrderAttribute : CsvFieldConfigurableBaseAttribute
{
    /// <summary>
    /// Order value of the CSV field. The default value is zero for unconfigured fields.
    /// Fields with a lower order value will be written before fields with a higher order value.
    /// Order of fields with the same order value is not guaranteed.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvOrderAttribute"/> class.
    /// </summary>
    /// <param name="order">Order value of the CSV field.</param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public CsvOrderAttribute(int order)
    {
        Order = order;
    }
}
