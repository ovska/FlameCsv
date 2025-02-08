using JetBrains.Annotations;

namespace FlameCsv.Attributes;

/// <summary>
/// Base class for attributes that can be used to configure a specific field or property.
/// </summary>
[PublicAPI]
public abstract class CsvFieldConfigurableBaseAttribute : CsvTypeConfigurableBaseAttribute
{
    private readonly string? _memberName;

    /// <summary>
    /// Name of the property, field, or parameter the attribute applies to.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
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
