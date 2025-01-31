using System.ComponentModel;
using JetBrains.Annotations;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Configures the target type for reading and writing CSV.
/// </summary>
/// <seealso cref="CsvOptions{T}.ReflectionAssemblies"/>
[PublicAPI]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class CsvAssemblyTypeAttribute : CsvTypeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvAssemblyTypeAttribute"/> to configure
    /// how a type is bound to a CSV when reading and writing CSV.
    /// </summary>
    /// <param name="targetType">Targeted type</param>
    public CsvAssemblyTypeAttribute(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        TargetType = targetType;
    }

    /// <summary>
    /// Type targeted by the attribute.
    /// </summary>
    public Type TargetType { get; }
}
