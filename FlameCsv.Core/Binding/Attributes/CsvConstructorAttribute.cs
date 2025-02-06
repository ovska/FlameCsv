using JetBrains.Annotations;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Marks the constructor to be used when creating instances while reading records.
/// If omitted and the type has only one public constructor, that constructor will be used.
/// Otherwise, an empty constructor will be used if one is available.
/// </summary>
/// <remarks>
/// This attribute is only used when reading CSV.
/// </remarks>
[PublicAPI]
[AttributeUsage(
    AttributeTargets.Constructor |
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Assembly,
    Inherited = false)]
public sealed class CsvConstructorAttribute : Attribute
{
    private Type[]? _parameterTypes;
    private Type? _targetType;

    /// <summary>
    /// When used on a class or struct, specifies the parameter types of the constructor to use.
    /// Has no effect when used directly on a constructor.
    /// </summary>
    public Type[] ParameterTypes
    {
        get => _parameterTypes!;
        init => _parameterTypes = value ?? throw new ArgumentNullException(nameof(ParameterTypes));
    }

    /// <summary>
    /// Type targeted by the attribute. Used when the attribute is applied to an assembly.
    /// </summary>
    public Type TargetType
    {
        get => _targetType!;
        init => _targetType = value ?? throw new ArgumentNullException(nameof(TargetType));
    }
}
