using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Converters.Text;
using FlameCsv.Utilities;

namespace FlameCsv;

/// <summary>
/// A configurable reader options with common built-in parsers.
/// </summary>
/// <remarks>
/// Initialized with the following parsers:
/// <list type="bullet">
/// <item><see cref="StringTextParser"/> or <see cref="PoolingStringTextParser"/></item>
/// <item><see cref="IntegerTextParser"/></item>
/// <item><see cref="BooleanTextConverter"/></item>
/// <item><see cref="DateTimeTextParser"/></item>
/// <item><see cref="EnumTextConverterFactory"/></item>
/// <item><see cref="NullableConverterFactory{T}"/></item>
/// <item><see cref="DecimalTextParser"/></item>
/// <item><see cref="GuidTextConverter"/></item>
/// <item><see cref="TimeSpanTextConverter"/></item>
/// <item><see cref="Base64TextParser"/></item>
/// <item><see cref="DateOnlyTextParser"/></item>
/// <item><see cref="TimeOnlyTextParser"/></item>
/// </list>
/// </remarks>
public sealed class CsvTextOptions : CsvOptions<char>
{
    private static readonly Lazy<CsvTextOptions> _default = new(() => new(isReadOnly: true));

    /// <summary>Returns a thread-safe read only singleton instance with default options.</summary>
    /// <remarks>Create a new instance if you need to configure the options or parsers.</remarks>
    public static CsvTextOptions Default => _default.Value;

    private StringPool? _stringPool;
    private IFormatProvider? _formatProvider;
    private NumberStyles _integerNumberStyles;
    private NumberStyles _decimalNumberStyles;
    private string? _integerFormat;
    private string? _decimalFormat;
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
    private TypeStringDictionary? _nullTokens;
    private TypeStringDictionary? _formats;

    /// <inheritdoc cref="CsvTextOptions"/>
    public CsvTextOptions() : this(false)
    {
    }

    public CsvTextOptions(CsvTextOptions other) : base(other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _stringPool = other._stringPool;
        _formatProvider = other._formatProvider;
        _integerNumberStyles = other._integerNumberStyles;
        _decimalNumberStyles = other._decimalNumberStyles;
        _integerFormat = other._integerFormat;
        _decimalFormat = other._decimalFormat;
        _dateTimeFormat = other._dateTimeFormat;
        _timeSpanFormat = other._timeSpanFormat;
        _dateOnlyFormat = other._dateOnlyFormat;
        _timeOnlyFormat = other._timeOnlyFormat;
        _dateTimeStyles = other._dateTimeStyles;
        _timeSpanStyles = other._timeSpanStyles;
        _guidFormat = other._guidFormat;
        _ignoreEnumCase = other._ignoreEnumCase;
        _allowUndefinedEnumValues = other._allowUndefinedEnumValues;
        _readEmptyStringsAsNull = other._readEmptyStringsAsNull;
        _null = other._null;

        // copy collections
        _booleanValues = other._booleanValues?.ToList();
        _nullTokens = new(this, other._nullTokens);
        _formats = new(this, other._formats);
    }

    private CsvTextOptions(bool isReadOnly)
    {
        _formatProvider = CultureInfo.InvariantCulture;
        _integerNumberStyles = NumberStyles.Integer;
        _decimalNumberStyles = NumberStyles.Float;

        _delimiter = ',';
        _quote = '"';
        _newline = "\r\n".AsMemory();

        if (isReadOnly)
            MakeReadOnly();
    }

    public CsvTextOptions Clone() => new(this);

    public char Delimiter
    {
        get => _delimiter;
        set => ((ICsvDialectOptions<char>)this).Delimiter = value;
    }

    public char Quote
    {
        get => _quote;
        set => ((ICsvDialectOptions<char>)this).Quote = value;
    }

    public string Newline
    {
        get => _newline.ToString();
        set => ((ICsvDialectOptions<char>)this).Newline = value.AsMemory();
    }

    public char? Escape
    {
        get => _escape;
        set => ((ICsvDialectOptions<char>)this).Escape = value;
    }

    public override IDictionary<Type, string?> NullTokens => _nullTokens ??= new(this);

    /// <summary>
    /// Overridden values for the 
    /// </summary>
    public IDictionary<Type, string?> Formats => _formats ??= new(this);

    /// <summary>
    /// String pool to use when parsing strings. Default is <see langword="null"/>, which results in no pooling.
    /// </summary>
    /// <remarks>
    /// Pooling reduces raw throughput, but can have profound impact on allocations
    /// if the data has a lot of repeating strings.
    /// </remarks>
    public StringPool? StringPool
    {
        get => _stringPool;
        set => this.SetValue(ref _stringPool, value);
    }

    /// <summary>
    /// Format provider passed by default to multiple parsers.
    /// Default is <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public IFormatProvider? FormatProvider
    {
        get => _formatProvider;
        set => this.SetValue(ref _formatProvider, value);
    }

    /// <summary>
    /// Used by <see cref="IntegerTextParser"/>. Default is <see cref="NumberStyles.Integer"/>.
    /// </summary>
    public NumberStyles IntegerNumberStyles
    {
        get => _integerNumberStyles;
        set
        {
            _ = int.TryParse("", value, null, out _); // validate styles
            this.SetValue(ref _integerNumberStyles, value);
        }
    }

    /// <summary>
    /// Used by <see cref="DecimalTextParser"/>. Default is <see cref="NumberStyles.Float"/>.
    /// </summary>
    public NumberStyles DecimalNumberStyles
    {
        get => _decimalNumberStyles;
        set
        {
            _ = double.TryParse("", value, null, out _); // validate styles
            this.SetValue(ref _decimalNumberStyles, value);
        }
    }

    public string? IntegerFormat
    {
        get => _integerFormat;
        set => this.SetValue(ref _integerFormat, value);
    }

    public string? DecimalFormat
    {
        get => _decimalFormat;
        set => this.SetValue(ref _decimalFormat, value);
    }

    /// <summary>
    /// Used by <see cref="DateTimeTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? DateTimeFormat
    {
        get => _dateTimeFormat;
        set => this.SetValue(ref _dateTimeFormat, value);
    }

    /// <summary>
    /// Used by <see cref="TimeSpanTextConverter"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? TimeSpanFormat
    {
        get => _timeSpanFormat;
        set => this.SetValue(ref _timeSpanFormat, value);
    }

    /// <summary>
    /// Used by <see cref="DateOnlyTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? DateOnlyFormat
    {
        get => _dateOnlyFormat;
        set => this.SetValue(ref _dateOnlyFormat, value);
    }

    /// <summary>
    /// Used by <see cref="TimeOnlyTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? TimeOnlyFormat
    {
        get => _timeOnlyFormat;
        set => this.SetValue(ref _timeOnlyFormat, value);
    }

    /// <summary>
    /// Styles passed to <see cref="DateTimeTextParser"/>. Default is
    /// <see cref="DateTimeStyles.None"/>.
    /// </summary>
    public DateTimeStyles DateTimeStyles
    {
        get => _dateTimeStyles;
        set
        {
            _ = DateTime.TryParse("", null, value, out _); // validate styles
            this.SetValue(ref _dateTimeStyles, value);
        }
    }

    /// <summary>
    /// Styles passed to <see cref="TimeSpanTextConverter"/>. Default is
    /// <see cref="TimeSpanStyles.None"/>.
    /// </summary>
    public TimeSpanStyles TimeSpanStyles
    {
        get => _timeSpanStyles;
        set
        {
            _ = TimeSpan.TryParseExact("", "", null, styles: value, out _); // validate styles
            this.SetValue(ref _timeSpanStyles, value);
        }
    }

    /// <summary>
    /// Used by <see cref="GuidTextConverter"/>. Default is null, which auto-detects the format.
    /// </summary>
    public string? GuidFormat
    {
        get => _guidFormat;
        set => this.SetValue(ref _guidFormat, value);
    }

    /// <summary>
    /// Used by <see cref="EnumTextConverter{TEnum}"/>. Default is <see langword="true"/>.
    /// </summary>
    public bool IgnoreEnumCase
    {
        get => _ignoreEnumCase;
        set => this.SetValue(ref _ignoreEnumCase, value);
    }

    /// <summary>
    /// Used by <see cref="Converters.Utf8.EnumUtf8Parser{TEnum}"/> to optionally skip validating that the parsed value is defined.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool AllowUndefinedEnumValues
    {
        get => _allowUndefinedEnumValues;
        set => this.SetValue(ref _allowUndefinedEnumValues, value);
    }

    /// <summary>
    /// Used by <see cref="StringTextParser"/> and <see cref="PoolingStringTextParser"/> to return nulls when a
    /// string field is empty. Default is <see langword="false"/>.
    /// </summary>
    public bool ReadEmptyStringsAsNull
    {
        get => _readEmptyStringsAsNull;
        set => this.SetValue(ref _readEmptyStringsAsNull, value);
    }

    /// <summary>
    /// Used by <see cref="NullableConverter{T,TValue}"/> when parsing nullable value types. Default is null/empty,
    /// which will return null for supported types on empty fields, or fields that are all whitespace.
    /// </summary>
    public string? Null
    {
        get => _null;
        set => this.SetValue(ref _null, value);
    }

    /// <summary>
    /// Optional custom boolean value mapping. Empty and null are equivalent. Default is <see langword="null"/>,
    /// which defers parsing to <see cref="bool.TryParse(ReadOnlySpan{char},out bool)"/>.
    /// </summary>
    public IReadOnlyCollection<(string text, bool value)>? BooleanValues
    {
        get => _booleanValues;
        set => this.SetValue(ref _booleanValues, value);
    }

    internal protected override bool TryGetDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<char>? converter)
    {
        if (DefaultConverters.Text.TryGetValue(type, out var factory))
        {
            converter = factory(this);
            return true;
        }

        if (EnumTextConverterFactory.Instance.CanConvert(type))
        {
            converter = EnumTextConverterFactory.Instance.Create(type, this);
            return true;
        }

        converter = default;
        return false;
    }

    public override IHeaderBinder<char> GetHeaderBinder() => new DefaultHeaderBinder<char>(this);

    public override string GetAsString(ReadOnlySpan<char> field) => field.ToString();

    public override ReadOnlyMemory<char> GetNullToken(Type resultType)
    {
        if (_nullTokens is not null && _nullTokens.TryGetInternalValue(resultType, out string? value))
            return value.AsMemory();

        return Null.AsMemory();
    }

    public override void WriteChars<TWriter>(TWriter writer, ReadOnlySpan<char> value)
    {
        if (!value.IsEmpty)
        {
            value.CopyTo(writer.GetSpan(value.Length));
            writer.Advance(value.Length);
        }
    }
}
