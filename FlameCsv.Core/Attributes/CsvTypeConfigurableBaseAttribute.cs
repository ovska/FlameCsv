using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Base class for attributes that can be used to configure a specific type.
/// </summary>
[PublicAPI]
public abstract class CsvTypeConfigurableBaseAttribute : Attribute
{
    private readonly Type? _targetType;

    /// <summary>
    /// Type targeted by the attribute. Used only when the attribute is applied to an assembly.
    /// </summary>
    public Type TargetType
    {
        get => _targetType!;
        init => _targetType = value ?? throw new ArgumentNullException(nameof(TargetType));
    }
}
