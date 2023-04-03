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
public sealed class CsvTextReaderOptions :
    CsvReaderOptions<char>,
    ICsvNullTokenProvider<char>
{
    private static readonly Lazy<CsvTextReaderOptions> _default = new(() => new(isReadOnly: true));

    /// <summary>Returns a thread-safe read only singleton instance with default options.</summary>
    /// <remarks>Create a new instance if you need to configure the options or parsers.</remarks>
    public static CsvTextReaderOptions Default => _default.Value;

    private StringPool? _stringPool;
    private IFormatProvider? _formatProvider;
    private NumberStyles _integerNumberStyles;
    private NumberStyles _decimalNumberStyles;
    private string? _dateTimeFormat;
    private string? _timeSpanFormat;
    private string? _dateOnlyFormat;
    private string? _timeOnlyFormat;
    private DateTimeStyles _dateTimeStyles;
    private TimeSpanStyles _timeSpanStyles;
    private string? _guidFormat;
    private bool _ignoreEnumCase;
    private bool _allowUndefinedEnumValues;
    private bool _readEmptyStringsAsNull;
    private string? _null;
    private IReadOnlyCollection<(string text, bool value)>? _booleanValues;
    private Dictionary<Type, ReadOnlyMemory<char>>? _nullOverrides;

    /// <inheritdoc cref="CsvTextReaderOptions"/>
    public CsvTextReaderOptions() : this(false)
    {
    }

    private CsvTextReaderOptions(bool isReadOnly)
    {
        _formatProvider = CultureInfo.InvariantCulture;
        _integerNumberStyles = NumberStyles.Integer;
        _decimalNumberStyles = NumberStyles.Float;

        _delimiter = ',';
        _quote = '"';
        _newline = "\r\n".AsMemory();
        _whitespace = ReadOnlyMemory<char>.Empty;

        if (isReadOnly)
            MakeReadOnly();
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
    /// Used by <see cref="EnumUtf8Parser{TEnum}"/> to optionally skip validating that the parsed value is defined.
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
    /// Used by <see cref="NullableParser{T,TValue}"/> when parsing nullable value types. Default is null/empty,
    /// which will return null for supported types on empty columns, or columns that are all whitespace.
    /// </summary>
    public string? Null
    {
        get => _null;
        set => SetValue(ref _null, value);
    }

    /// <summary>
    /// Optional custom boolean value mapping. Empty and null are equivalent. Default is <see langword="null"/>,
    /// which defers parsing to <see cref="bool.TryParse(ReadOnlySpan{char},out bool)"/>.
    /// </summary>
    public IReadOnlyCollection<(string text, bool value)>? BooleanValues
    {
        get => _booleanValues;
        set => SetValue(ref _booleanValues, value);
    }

    /// <summary>
    /// Overridden values that match to null when parsing <see cref="Nullable{T}"/>
    /// instead of the default <see cref="Null"/>.
    /// </summary>
    public IDictionary<Type, ReadOnlyMemory<char>> NullOverrides => _nullOverrides ??= new();

    /// <inheritdoc/>
    protected override IEnumerable<ICsvParser<char>> GetDefaultParsers()
    {
        // sorted in assumed reverse order of usefulness
        return new ICsvParser<char>[]
        {
            TimeOnlyTextParser.GetOrCreate(TimeOnlyFormat, DateTimeStyles, FormatProvider),
            DateOnlyTextParser.GetOrCreate(DateOnlyFormat, DateTimeStyles, FormatProvider),
            GuidTextParser.GetOrCreate(GuidFormat),
            Base64TextParser.Instance,
            TimeSpanTextParser.GetOrCreate(TimeSpanFormat,  TimeSpanStyles, FormatProvider),
            NullableParserFactory<char>.GetOrCreate(Null.AsMemory()),
            new EnumTextParserFactory(AllowUndefinedEnumValues, IgnoreEnumCase),
            DateTimeTextParser.GetOrCreate(DateTimeFormat, DateTimeStyles, FormatProvider),
            DecimalTextParser.GetOrCreate(FormatProvider, DecimalNumberStyles),
            BooleanTextParser.GetOrCreate(BooleanValues),
            IntegerTextParser.GetOrCreate(FormatProvider, IntegerNumberStyles),
            StringPool is null
                ? StringTextParser.GetOrCreate(ReadEmptyStringsAsNull)
                : PoolingStringTextParser.GetOrCreate(StringPool, ReadEmptyStringsAsNull),
        };
    }

    ReadOnlyMemory<char> ICsvNullTokenProvider<char>.Default => Null.AsMemory();

    bool ICsvNullTokenProvider<char>.TryGetOverride(Type type, out ReadOnlyMemory<char> value)
    {
        if (_nullOverrides is not null)
            return _nullOverrides.TryGetValue(type, out value);

        value = default;
        return false;
    }
}
