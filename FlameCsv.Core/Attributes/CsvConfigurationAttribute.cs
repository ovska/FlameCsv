using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Base class for attributes that can be used to configure a specific type.<br/>
/// When placed on an assembly, <see cref="TargetType"/> must be set.
/// </summary>
[PublicAPI]
public abstract class CsvConfigurationAttribute : Attribute
{
    private readonly Type? _targetType;

    /// <summary>
    /// Type targeted by the attribute. Used only when the attribute is applied to an assembly.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if the value is null</exception>
    /// <remarks>Must be set if this attribute is placed on an assembly.</remarks>
    public Type TargetType
    {
        get => _targetType!;
        init => _targetType = value ?? throw new ArgumentNullException(nameof(TargetType));
    }
}
