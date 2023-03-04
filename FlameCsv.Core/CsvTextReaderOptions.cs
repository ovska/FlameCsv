using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;

namespace FlameCsv;

/// <summary>
/// A configurable reader options with common built-in parsers.
/// </summary>
/// <remarks>
/// Initialized with the following parsers:
/// <list type="bullet">
/// <item><see cref="StringTextParser"/> or <see cref="PoolingStringTextParser"/></item>
/// <item><see cref="IntegerTextParser"/></item>
/// <item><see cref="BooleanTextParser"/></item>
/// <item><see cref="DateTimeTextParser"/></item>
/// <item><see cref="EnumTextParserFactory"/></item>
/// <item><see cref="NullableParserFactory{T}"/></item>
/// <item><see cref="DecimalTextParser"/></item>
/// <item><see cref="GuidTextParser"/></item>
/// <item><see cref="TimeSpanTextParser"/></item>
/// <item><see cref="Base64TextParser"/></item>
/// <item><see cref="DateOnlyTextParser"/></item>
/// <item><see cref="TimeOnlyTextParser"/></item>
/// </list>
/// </remarks>
public sealed class CsvTextReaderOptions : CsvReaderOptions<char>
{
    /// <summary>
    /// Returns a thread-safe read only singleton instance with default options.
    /// </summary>
    public static new CsvTextReaderOptions Default => CsvReaderOptionsDefaults.Text;

    public StringPool? _stringPool;
    public IFormatProvider? _formatProvider = CultureInfo.InvariantCulture;
    public NumberStyles _integerNumberStyles = NumberStyles.Integer;
    public NumberStyles _decimalNumberStyles = NumberStyles.Float;
    public string? _dateTimeFormat;
    public string? _timeSpanFormat;
    public string? _dateOnlyFormat;
    public string? _timeOnlyFormat;
    public DateTimeStyles _dateTimeStyles;
    public TimeSpanStyles _timeSpanStyles;
    public string? _guidFormat;
    public bool _ignoreEnumCase;
    public bool _allowUndefinedEnumValues;
    public bool _readEmptyStringsAsNull;
    public string? _null;
    public IReadOnlyCollection<(string text, bool value)>? _booleanValues;

    /// <inheritdoc cref="CsvTextReaderOptions"/>
    public CsvTextReaderOptions()
    {
    }

    /// <summary>
    /// String pool to use by default for all strings. If set, <see cref="PoolingStringTextParser"/> is used
    /// as the default string parser.<br/>
    /// Default is <see langword="null"/>, which results in no pooling and <see cref="StringTextParser"/> as the default.
    /// </summary>
    /// <remarks>
    /// Pooling reduces raw throughput, but can have profound impact on allocations
    /// if the data has a lot of repeating strings.
    /// </remarks>
    public StringPool? StringPool
    {
        get => _stringPool;
        set => SetValue(ref _stringPool, value);
    }

    /// <summary>
    /// Format provider passed by default to multiple parsers.
    /// Default is <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public IFormatProvider? FormatProvider
    {
        get => _formatProvider;
        set => SetValue(ref _formatProvider, value);
    }

    /// <summary>
    /// Used by <see cref="IntegerTextParser"/>. Default is <see cref="NumberStyles.Integer"/>.
    /// </summary>
    public NumberStyles IntegerNumberStyles
    {
        get => _integerNumberStyles;
        set => SetValue(ref _integerNumberStyles, value);
    }

    /// <summary>
    /// Used by <see cref="DecimalTextParser"/>. Default is <see cref="NumberStyles.Float"/>.
    /// </summary>
    public NumberStyles DecimalNumberStyles
    {
        get => _decimalNumberStyles;
        set => SetValue(ref _decimalNumberStyles, value);
    }

    /// <summary>
    /// Used by <see cref="DateTimeTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? DateTimeFormat
    {
        get => _dateTimeFormat;
        set => SetValue(ref _dateTimeFormat, value);
    }

    /// <summary>
    /// Used by <see cref="TimeSpanTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? TimeSpanFormat
    {
        get => _timeSpanFormat;
        set => SetValue(ref _timeSpanFormat, value);
    }

    /// <summary>
    /// Used by <see cref="DateOnlyTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? DateOnlyFormat
    {
        get => _dateOnlyFormat;
        set => SetValue(ref _dateOnlyFormat, value);
    }

    /// <summary>
    /// Used by <see cref="TimeOnlyTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? TimeOnlyFormat
    {
        get => _timeOnlyFormat;
        set => SetValue(ref _timeOnlyFormat, value);
    }

    /// <summary>
    /// Styles passed to <see cref="DateTimeTextParser"/>. Default is
    /// <see cref="DateTimeStyles.None"/>.
    /// </summary>
    public DateTimeStyles DateTimeStyles
    {
        get => _dateTimeStyles;
        set => SetValue(ref _dateTimeStyles, value);
    }

    /// <summary>
    /// Styles passed to <see cref="TimeSpanTextParser"/>. Default is
    /// <see cref="TimeSpanStyles.None"/>.
    /// </summary>
    public TimeSpanStyles TimeSpanStyles
    {
        get => _timeSpanStyles;
        set => SetValue(ref _timeSpanStyles, value);
    }

    /// <summary>
    /// Used by <see cref="GuidTextParser"/>. Default is null, which auto-detects the format.
    /// </summary>
    public string? GuidFormat
    {
        get => _guidFormat;
        set => SetValue(ref _guidFormat, value);
    }

    /// <summary>
    /// Used by <see cref="EnumTextParser{TEnum}"/>. Default is <see langword="true"/>.
    /// </summary>
    public bool IgnoreEnumCase
    {
        get => _ignoreEnumCase;
        set => SetValue(ref _ignoreEnumCase, value);
    }

    /// <summary>
    /// Used by <see cref="EnumTextParser{TEnum}"/> to optionally validate that the parsed value is defined.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool AllowUndefinedEnumValues
    {
        get => _allowUndefinedEnumValues;
        set => SetValue(ref _allowUndefinedEnumValues, value);
    }

    /// <summary>
    /// Used by <see cref="StringTextParser"/> and <see cref="PoolingStringTextParser"/> to return nulls when a
    /// string column is empty. Default is <see langword="false"/>.
    /// </summary>
    public bool ReadEmptyStringsAsNull
    {
        get => _readEmptyStringsAsNull;
        set => SetValue(ref _readEmptyStringsAsNull, value);
    }

    /// <summary>
    /// Used by <see cref="NullableParser{T,TValue}"/> when parsing nullable value types.
    /// Default is null/empty, which will return null for supported types on empty columns, or columns that are all whitespace.
    /// </summary>
    public string? Null
    {
        get => _null;
        set => SetValue(ref _null, value);
    }

    /// <summary>
    /// Optional custom boolean value mapping. If not null, must not be empty. Default is <see langword="null"/>,
    /// which defers parsing to <see cref="bool.TryParse(ReadOnlySpan{char},out bool)"/>.
    /// </summary>
    public IReadOnlyCollection<(string text, bool value)>? BooleanValues
    {
        get => _booleanValues;
        set => SetValue(ref _booleanValues, value);
    }

    protected override IEnumerable<ICsvParser<char>> GetDefaultParsers()
    {
        // sorted in assumed reverse order of usefulness
        return new ICsvParser<char>[]
        {
            new TimeOnlyTextParser(TimeOnlyFormat, DateTimeStyles, FormatProvider),
            new DateOnlyTextParser(DateOnlyFormat, DateTimeStyles, FormatProvider),
            new GuidTextParser(GuidFormat),
            new Base64TextParser(),
            new TimeSpanTextParser(TimeSpanFormat, FormatProvider, TimeSpanStyles),
            new NullableParserFactory<char>(Null.AsMemory()),
            new EnumTextParserFactory(AllowUndefinedEnumValues, IgnoreEnumCase),
            new DateTimeTextParser(DateTimeFormat, FormatProvider, DateTimeStyles),
            new DecimalTextParser(DecimalNumberStyles, FormatProvider),
            new BooleanTextParser(BooleanValues),
            new IntegerTextParser(IntegerNumberStyles, FormatProvider),
            StringPool is { } stringPool
                ? new PoolingStringTextParser(stringPool, ReadEmptyStringsAsNull)
                : new StringTextParser(ReadEmptyStringsAsNull),
        };
    }
}
