namespace FlameCsv.Parsers.Utf8;

/// <summary>
/// Represents the configuration for the built-in UTF8 parsers.
/// </summary>
public class CsvUtf8ParsersConfig
{
    internal static readonly CsvUtf8ParsersConfig Default = new();

    /// <summary>
    /// Used by <see cref="IntegerUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public virtual char IntegerFormat { get; set; } = '\0';

    /// <summary>
    /// Used by <see cref="DecimalUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public virtual char DecimalFormat { get; set; } = '\0';

    /// <summary>
    /// Used by <see cref="DateTimeUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public virtual char DateTimeFormat { get; set; } = '\0';

    /// <summary>
    /// Used by <see cref="TimeSpanUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public virtual char TimeSpanFormat { get; set; } = '\0';

    /// <summary>
    /// Used by <see cref="GuidUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public virtual char GuidFormat { get; set; } = '\0';

    /// <summary>
    /// Used by <see cref="EnumUtf8Parser{TEnum}"/>. Default is <see langword="true"/>.
    /// </summary>
    public virtual bool IgnoreEnumCase { get; set; } = true;

    /// <summary>
    /// Used by <see cref="EnumUtf8Parser{TEnum}"/> to optionally validate that the parsed value is defined.
    /// Default is <see langword="false"/>.
    /// </summary>
    public virtual bool AllowUndefinedEnumValues { get; set; }

    /// <summary>
    /// Used by <see cref="NullableParser{T,TValue}"/> when parsing nullable value types.
    /// Default is empty, which will return null for empty columns or columns that are all whitespace.
    /// </summary>
    public virtual ReadOnlyMemory<byte> Null { get; set; }

    /// <summary>
    /// Optional custom boolean value mapping. If not null, must not be empty.
    /// Default is <see langword="null"/>, which defers parsing to <see cref="System.Buffers.Text.Utf8Parser"/>.
    /// </summary>
    public virtual IReadOnlyCollection<(ReadOnlyMemory<byte> bytes, bool value)>? BooleanValues { get; set; } = null;
}
