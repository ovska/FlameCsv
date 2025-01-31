using JetBrains.Annotations;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Configures the target property, field, or parameter.
/// </summary>
/// <remarks>
/// Intended to be used on types that cannot be configured directly with <see cref="CsvFieldAttribute"/>.<br/>
/// When placed on an interface, <see cref="CsvTypeAttribute.CreatedTypeProxy"/> must be set when reading.
/// </remarks>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true)]
public class CsvTypeFieldAttribute : CsvFieldAttribute
{
    /// <summary>
    /// Name of the property, field or parameter this attribute applies to (see <see cref="IsParameter"/>).
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// <c>true</c> if <see cref="MemberName"/> points to a parameter in a constructor marked with
    /// <see cref="CsvConstructorAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Parameters can only be ignored if they have a default value.
    /// </remarks>
    public bool IsParameter { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvTypeFieldAttribute"/> to configure how a property, field,
    /// or parameter is bound to a CSV field when reading and writing CSV.
    /// </summary>
    /// <param name="memberName">
    /// Name of the property, field or parameter this attribute applies to (see <see cref="IsParameter"/>).
    /// </param>
    /// <param name="headers">
    /// Header names matched to this member/parameter. If empty, the member/parameter name is used.<br/>
    /// When writing, the first value is used.
    /// </param>
    public CsvTypeFieldAttribute(string memberName, params string[] headers) : base(headers)
    {
        ArgumentException.ThrowIfNullOrEmpty(memberName);
        MemberName = memberName;
    }
}
