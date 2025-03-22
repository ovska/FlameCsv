using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Flags for configuring enum parsing and formatting.
/// </summary>
/// <example>
/// <code>
/// var options = new CsvOptions&lt;char&gt;
/// {
///     EnumOptions = CsvEnumOptions.IgnoreCase | CsvEnumOptions.UseEnumMemberAttribute
/// };
/// </code>
/// </example>
[Flags]
[PublicAPI]
public enum CsvEnumOptions
{
    /// <summary>
    /// Enums are parsed and formatted using the default behavior, and undefined values are not allowed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Ignore case when parsing enum values.
    /// </summary>
    IgnoreCase = 1 << 0,

    /// <summary>
    /// Enum values are allowed that are not defined in the enum type (<see cref="Enum.IsDefined"/> is not checked).
    /// </summary>
    AllowUndefinedValues = 1 << 1,

    /// <summary>
    /// Support <see cref="System.Runtime.Serialization.EnumMemberAttribute"/> when parsing enum values.
    /// If the string-format <c>"G"</c> is configured, the enum member value is used when writing as well.
    /// </summary>
    /// <remarks>
    /// This option is ignored on enums with <see cref="FlagsAttribute"/>, or when the format is <c>"F"</c>.
    /// </remarks>
    UseEnumMemberAttribute = 1 << 2,
}
