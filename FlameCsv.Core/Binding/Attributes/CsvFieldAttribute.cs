using JetBrains.Annotations;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Configures the property, field, or parameter.
/// </summary>
/// <remarks>
/// Multiple instances of the attribute can be used with a different <see cref="Scope"/>. Multiple instances
/// of the same scope are not supported.
/// </remarks>
[PublicAPI]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class CsvFieldAttribute : Attribute
{
    /// <summary>
    /// Zero-based field index when reading or writing headerless CSV, or -1 if not set.
    /// </summary>
    public int Index
    {
        get => _index;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _index = value;
        }
    }

    private readonly int _index = -1;

    /// <summary>
    /// Order in which to write the value, or to match it to a header.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// The member/parameter is ignored when reading or writing CSV.<br/>
    /// If <c>true</c>, other configurations in this attribute instance have no effect.
    /// </summary>
    /// <remarks>
    /// Parameters can only be ignored if they have a default value.
    /// </remarks>
    public bool IsIgnored { get; init; }

    /// <summary>
    /// The member/parameter is required to match to a header when reading CSV.
    /// </summary>
    /// <remarks>
    /// Parameters are always required unless they have a default value.
    /// </remarks>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Header names matched to this member/parameter. If empty, the member/parameter name is used.<br/>
    /// When writing, the first value is used.
    /// </summary>
    public string[] Headers { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvFieldAttribute"/> to configure how a property, field,
    /// or parameter is bound to a CSV field when reading and writing CSV.
    /// </summary>
    /// <param name="headers">
    /// Header names matched to this member/parameter. If empty, the member/parameter name is used.<br/>
    /// When writing, the first value is used.
    /// </param>
    public CsvFieldAttribute(params string[] headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        for (int i = 0; i < headers.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(headers[i]);
        }

        Headers = headers;
    }
}
