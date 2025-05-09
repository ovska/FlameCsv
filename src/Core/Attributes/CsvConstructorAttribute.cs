using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Defines the constructor to be used when creating instances while reading records.<br/>
/// </summary>
/// <remarks>
/// Assembly attributes override class attributes, which override attributes directly on the constructor.
/// </remarks>
[PublicAPI]
[AttributeUsage(
    AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly,
    AllowMultiple = true, // type and assembly need AllMultiple = true
    Inherited = false
)]
public sealed class CsvConstructorAttribute : CsvConfigurationAttribute
{
    private Type[]? _parameterTypes;

    /// <summary>
    /// Specifies the parameter types of the constructor to use.<br/>
    /// Has no effect when used directly on a constructor.
    /// </summary>
    public Type[] ParameterTypes
    {
        get => _parameterTypes!;
        init => _parameterTypes = value ?? throw new ArgumentNullException(nameof(ParameterTypes));
    }
}
