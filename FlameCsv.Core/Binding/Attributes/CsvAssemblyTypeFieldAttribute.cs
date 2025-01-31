using System.ComponentModel;
using JetBrains.Annotations;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Configures the target type's property, field, or parameter.
/// </summary>
/// <remarks>
/// Intended to be used on third party types that cannot be configured directly with <see cref="CsvTypeFieldAttribute"/>.
/// <br/>When targeting an interface, <see cref="CsvTypeAttribute.CreatedTypeProxy"/> must be set when reading.
/// </remarks>
/// <seealso cref="CsvOptions{T}.ReflectionAssemblies"/>
[PublicAPI]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class CsvAssemblyTypeFieldAttribute : CsvTypeFieldAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CsvAssemblyTypeFieldAttribute"/> to configure how a property,
    /// field, or parameter is bound to a CSV field when reading and writing CSV.
    /// </summary>
    /// <param name="targetType">Type the attribute applies to</param>
    /// <param name="memberName">
    /// Name of the property, field or parameter this attribute applies to
    /// (see <see cref="CsvTypeFieldAttribute.IsParameter"/>).
    /// </param>
    /// <param name="headers">
    /// Header names matched to this member/parameter. If empty, the member/parameter name is used.<br/>
    /// When writing, the first value is used.
    /// </param>
    public CsvAssemblyTypeFieldAttribute(Type targetType, string memberName, params string[] headers)
        : base(memberName, headers)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        TargetType = targetType;
    }

    /// <summary>
    /// Type targeted by the attribute.
    /// </summary>
    public Type TargetType { get; }
}
