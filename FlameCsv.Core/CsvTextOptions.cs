using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Converters.Text;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv;

public class CsvTextOptions : CsvOptions<char>, IGetOrCreate<char, CsvTextOptions>
{
    private static readonly Lazy<CsvTextOptions> _default = new(() => new(isReadOnly: true));

    /// <summary>Returns a thread-safe read only singleton instance with default options.</summary>
    /// <remarks>Create a new instance if you need to configure the options or parsers.</remarks>
    public static CsvTextOptions Default => _default.Value;

    private IFormatProvider? _formatProvider;
    private NumberStyles _integerNumberStyles;
    private NumberStyles _decimalNumberStyles;
    private string? _integerFormat;
    private string? _decimalFormat;
    private string? _dateTimeFormat;
    private string? _timeSpanFormat;
    private string? _dateOnlyFormat;
    private string? _timeOnlyFormat;
    private string? _enumFormat;
    private DateTimeStyles _dateTimeStyles;
    private TimeSpanStyles _timeSpanStyles;
    private string? _guidFormat;
    private string? _null;
    private TypeDictionary<string?>? _nullTokens;

    /// <inheritdoc cref="CsvTextOptions"/>
    public CsvTextOptions() : this(false)
    {
    }

    public CsvTextOptions(CsvTextOptions other) : base(other)
    {
        ArgumentNullException.ThrowIfNull(other);

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
        _null = other._null;

        // copy collections
        _booleanValues = other._booleanValues?.ToList();
        _nullTokens = new(this, other._nullTokens);
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

    /// <inheritdoc cref="ICsvDialectOptions{T}.Delimiter"/>
    public char Delimiter { get => _delimiter; set => this.SetValue(ref _delimiter, value); }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Quote"/>
    public char Quote { get => _quote; set => this.SetValue(ref _quote, value); }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Newline"/>
    public string Newline { get => _newline.ToString(); set => this.SetValue(ref _newline, value.AsMemory()); }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Whitespace"/>
    public string? Whitespace { get => _whitespace.ToString(); set => this.SetValue(ref _whitespace, value.AsMemory()); }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Escape"/>
    public char? Escape { get => _escape; set => this.SetValue(ref _escape, value); }

    /// <inheritdoc/>
    public IDictionary<Type, string?> NullTokens => _nullTokens ??= new TypeDictionary<string?>(this);

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
        set
        {
            _ = 0.TryFormat(Span<char>.Empty, out _, value, null);
            this.SetValue(ref _integerFormat, value);
        }
    }

    public string? DecimalFormat
    {
        get => _decimalFormat;
        set
        {
            _ = 0d.TryFormat(Span<char>.Empty, out _, value, null);
            this.SetValue(ref _decimalFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="DateTimeTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? DateTimeFormat
    {
        get => _dateTimeFormat;
        set
        {
            _ = default(DateTimeOffset).TryFormat(Span<char>.Empty, out _, value, null);
            this.SetValue(ref _dateTimeFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="TimeSpanTextConverter"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? TimeSpanFormat
    {
        get => _timeSpanFormat;
        set
        {
            _ = default(TimeSpan).TryFormat(Span<char>.Empty, out _, value, null);
            this.SetValue(ref _timeSpanFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="DateOnlyTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? DateOnlyFormat
    {
        get => _dateOnlyFormat;
        set
        {
            _ = default(DateOnly).TryFormat(Span<char>.Empty, out _, value, null);
            this.SetValue(ref _dateOnlyFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="TimeOnlyTextParser"/>. Set to non-null to use exact parsing.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? TimeOnlyFormat
    {
        get => _timeOnlyFormat;
        set
        {
            _ = default(TimeOnly).TryFormat(Span<char>.Empty, out _, value, null);
            this.SetValue(ref _timeOnlyFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="EnumTextConverter{TEnum}"/>.
    /// Default is <see langword="null"/>.
    /// </summary>
    public string? EnumFormat
    {
        get => _enumFormat;
        set
        {
            _ = ((ISpanFormattable)NumberStyles.None).TryFormat(default, out _, value, null); // validate format
            this.SetValue(ref _enumFormat, value);
        }
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
        set
        {
            _ = Guid.TryParseExact(default, value, out _); // validate format
            this.SetValue(ref _guidFormat, value);
        }
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

    internal protected override bool TryGetDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<char>? converter)
    {
        if (EnumTextConverterFactory.Instance.CanConvert(type))
        {
            converter = EnumTextConverterFactory.Instance.Create(type, this);
            return true;
        }

        if (DefaultConverters.Text.Value.TryGetValue(type, out var factory))
        {
            converter = factory(this);
            return true;
        }
        converter = default;
        return false;
    }

    public override IHeaderBinder<char> GetHeaderBinder() => new DefaultHeaderBinder<char>(this);

    public override string GetAsString(ReadOnlySpan<char> field) => field.ToString();

    public override ReadOnlyMemory<char> GetNullToken(Type resultType)
    {
        if (_nullTokens is not null && _nullTokens.TryGetValue(resultType, out string? value))
            return value.AsMemory();

        return Null.AsMemory();
    }

    public override bool TryWriteChars(ReadOnlySpan<char> value, Span<char> destination, out int charsWritten)
    {
        return value.TryWriteTo(destination, out charsWritten);
    }

    public override bool TryGetChars(ReadOnlySpan<char> field, Span<char> destination, out int charsWritten)
    {
        return field.TryWriteTo(destination, out charsWritten);
    }

    public CsvConverter<char, TValue> GetOrCreate<TValue>(Func<CsvTextOptions, CsvConverter<char, TValue>> func)
    {
        CsvConverter<char>? converter = TryGetExistingOrExplicit(typeof(TValue), out bool created);

        if (converter is null)
        {
            converter = func(this);
            created = true;
        }

        if (created && !_converterCache.TryAdd(typeof(TValue), converter))
        {
            // ensure we return the same instance that was cached
            converter = (CsvConverter<char, TValue>)_converterCache[typeof(TValue)];
        }

        return (CsvConverter<char, TValue>)converter;
    }
}
