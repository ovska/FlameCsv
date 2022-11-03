using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Parsers.Text;

/// <summary>
/// Represents the configuration for the built-in text parsers.
/// </summary>
public class CsvTextParsersConfig
{
    internal static readonly CsvTextParsersConfig Default = new();

    /// <summary>
    /// FormatProvider passed by default to multiple parsers.
    /// Default is <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public virtual IFormatProvider? FormatProvider { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// Used by <see cref="IntegerTextParser"/>. Default is <see cref="System.Globalization.NumberStyles.Integer"/>.
    /// </summary>
    public virtual NumberStyles IntegerNumberStyles { get; set; } = NumberStyles.Integer;

    /// <summary>
    /// Used by <see cref="DecimalTextParser"/>. Default is <see cref="System.Globalization.NumberStyles.Float"/>.
    /// </summary>
    public virtual NumberStyles DecimalNumberStyles { get; set; } = NumberStyles.Float;

    /// <summary>
    /// Used by <see cref="DateTimeTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public virtual string? DateTimeFormat { get; set; }

    /// <summary>
    /// Used by <see cref="TimeSpanTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public virtual string? TimeSpanFormat { get; set; }

    /// <summary>
    /// Used by <see cref="DateOnlyTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public virtual string? DateOnlyFormat { get; set; }

    /// <summary>
    /// Used by <see cref="TimeOnlyTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public virtual string? TimeOnlyFormat { get; set; }

    /// <summary>
    /// Styles passed to <see cref="DateTimeTextParser"/>. Default is
    /// <see cref="System.Globalization.DateTimeStyles.None"/>.
    /// </summary>
    public virtual DateTimeStyles DateTimeStyles { get; set; } = DateTimeStyles.None;

    /// <summary>
    /// Styles passed to <see cref="TimeSpanTextParser"/>. Default is
    /// <see cref="System.Globalization.TimeSpanStyles.None"/>.
    /// </summary>
    public virtual TimeSpanStyles TimeSpanStyles { get; set; } = TimeSpanStyles.None;

    /// <summary>
    /// Used by <see cref="GuidTextParser"/>. Default is null, which auto-detects the format.
    /// </summary>
    public virtual string? GuidFormat { get; set; }

    /// <summary>
    /// Used by <see cref="EnumTextParser{TEnum}"/>. Default is <see langword="true"/>.
    /// </summary>
    public virtual bool IgnoreEnumCase { get; set; } = true;

    /// <summary>
    /// Used by <see cref="EnumTextParser{TEnum}"/> to optionally validate that the parsed value is defined.
    /// Default is <see langword="false"/>.
    /// </summary>
    public virtual bool AllowUndefinedEnumValues { get; set; }

    /// <summary>
    /// Used by <see cref="StringTextParser"/> and <see cref="PoolingStringTextParser"/> to return nulls when a
    /// string column is empty. Default is <see langword="false"/>.
    /// </summary>
    public virtual bool ReadEmptyStringsAsNull { get; set; }

    /// <summary>
    /// Used by <see cref="PoolingStringTextParser"/>. Set to non-null value to use string pooling globally.
    /// Pooling reduces string parsing throughput, but reduces allocations when there are a lot of common strings
    /// in the data. Default is <see langword="null"/>.
    /// </summary>
    public virtual StringPool? StringPool { get; set; }

    /// <summary>
    /// Used by <see cref="NullableParser{T,TValue}"/> when parsing nullable value types.
    /// Default is null/empty, which will return null for empty columns or columns that are all whitespace.
    /// </summary>
    public virtual string? Null { get; set; }

    /// <summary>
    /// Optional custom boolean value mapping. If not null, must not be empty. Default is <see langword="null"/>,
    /// which defers parsing to <see cref="bool.TryParse(System.ReadOnlySpan{char},out bool)"/>.
    /// </summary>
    public virtual IReadOnlyCollection<(string text, bool value)>? BooleanValues { get; set; } = null;
}
