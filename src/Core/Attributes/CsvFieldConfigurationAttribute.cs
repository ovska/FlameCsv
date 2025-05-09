using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Base class for attributes that can be used to configure a specific property, field, or parameter.<br/>
/// When not placed directly on a member/parameter, <see cref="MemberName"/> must be set.
/// </summary>
[PublicAPI]
public abstract class CsvFieldConfigurationAttribute : CsvConfigurationAttribute
{
    private readonly string? _memberName;

    /// <summary>
    /// Name of the property, field, or parameter the attribute applies to.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if the value is null</exception>
    /// <remarks>Must be set if this attribute is not placed directly on a property, field, or parameter.</remarks>
    /// <seealso cref="IsParameter"/>
    public string MemberName
    {
        get => _memberName!;
        init => _memberName = value ?? throw new ArgumentNullException(nameof(MemberName));
    }

    /// <summary>
    /// Whether <see cref="MemberName"/> points to a parameter.
    /// </summary>
    public bool IsParameter { get; init; }
}
