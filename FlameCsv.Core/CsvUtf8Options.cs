using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Utilities;

namespace FlameCsv;

public class CsvUtf8Options : CsvOptions<byte>, IGetOrCreate<byte, CsvUtf8Options>
{
    private static readonly Lazy<CsvUtf8Options> _default = new(() => new(isReadOnly: true));

    /// <summary>Returns a thread-safe read only singleton instance with default options.</summary>
    /// <remarks>Create a new instance if you need to configure the options or parsers.</remarks>
    public static CsvUtf8Options Default => _default.Value;

    private StandardFormat _booleanFormat = 'l';
    private StandardFormat _integerFormat;
    private StandardFormat _decimalFormat;
    private StandardFormat _dateTimeFormat;
    private StandardFormat _timeSpanFormat;
    private StandardFormat _guidFormat;
    private Utf8String _null;
    private TypeDictionary<Utf8String>? _nullTokens;

    /// <inheritdoc cref="CsvTextOptions"/>
    public CsvUtf8Options() : this(false)
    {
    }

    public CsvUtf8Options(CsvUtf8Options other) : base(other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _booleanFormat = other._booleanFormat;
        _integerFormat = other._integerFormat;
        _decimalFormat = other._decimalFormat;
        _dateTimeFormat = other._dateTimeFormat;
        _timeSpanFormat = other._timeSpanFormat;
        _guidFormat = other._guidFormat;
        _null = other._null;

        // copy collections
        _booleanValues = other._booleanValues?.ToList();
        _nullTokens = new(this, other._nullTokens);
    }

    private CsvUtf8Options(bool isReadOnly)
    {
        _delimiter = new Utf8Char(',');
        _quote = new Utf8Char('"');
        _newline = Utf8String.CRLF;

        if (isReadOnly)
            MakeReadOnly();
    }

    public CsvUtf8Options Clone() => new(this);

    /// <inheritdoc cref="ICsvDialectOptions{T}.Delimiter"/>
    public Utf8Char Delimiter { get => _delimiter; set => this.SetValue(ref _delimiter, value); }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Quote"/>
    public Utf8Char Quote { get => _quote; set => this.SetValue(ref _quote, value); }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Newline"/>
    public Utf8String Newline { get => _newline; set => this.SetValue(ref _newline, value); }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Whitespace"/>
    public Utf8String Whitespace { get => _whitespace; set => this.SetValue(ref _whitespace, value); }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Escape"/>
    public Utf8Char? Escape { get => _escape; set => this.SetValue(ref _escape, value); }

    public IDictionary<Type, Utf8String> NullTokens => _nullTokens ??= new TypeDictionary<Utf8String>(this);

    /// <summary>
    /// Used by <see cref="BooleanUtf8Converter"/> when writing booleans.
    /// Default is <c>'l'</c>.
    /// </summary>
    /// <remarks>
    /// Ignored if <see cref="BooleanValues"/> is not empty.
    /// </remarks>
    public StandardFormat BooleanFormat
    {
        get => _booleanFormat;
        set
        {
            // validate
            _ = Utf8Parser.TryParse(default, out bool _, out _, value.Symbol);
            _ = Utf8Formatter.TryFormat(default(bool), default, out _, value);

            this.SetValue(ref _booleanFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="IntegerUtf8Parser"/>.
    /// </summary>
    public StandardFormat IntegerFormat
    {
        get => _integerFormat;
        set
        {
            // validate
            _ = Utf8Parser.TryParse(default, out long _, out _, value.Symbol);
            _ = Utf8Formatter.TryFormat(default(long), default, out _, value);

            this.SetValue(ref _integerFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="DecimalUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public StandardFormat DecimalFormat
    {
        get => _decimalFormat;
        set
        {
            // validate
            _ = Utf8Parser.TryParse(default, out double _, out _, value.Symbol);
            _ = Utf8Formatter.TryFormat(default(double), default, out _, value);

            this.SetValue(ref _decimalFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="DateTimeUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public StandardFormat DateTimeFormat
    {
        get => _dateTimeFormat;
        set
        {
            // validate
            _ = Utf8Parser.TryParse(default, out DateTimeOffset _, out _, value.Symbol);
            _ = Utf8Formatter.TryFormat(default(DateTimeOffset), default, out _, value);

            this.SetValue(ref _dateTimeFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="TimeSpanUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public StandardFormat TimeSpanFormat
    {
        get => _timeSpanFormat;
        set
        {
            // validate
            _ = Utf8Parser.TryParse(default, out TimeSpan _, out _, value.Symbol);
            _ = Utf8Formatter.TryFormat(default(TimeSpan), default, out _, value);

            this.SetValue(ref _timeSpanFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="GuidUtf8Converter"/>.
    /// </summary>
    public StandardFormat GuidFormat
    {
        get => _guidFormat;
        set
        {
            // validate
            _ = Utf8Parser.TryParse(default, out Guid _, out _, value.Symbol);
            _ = Utf8Formatter.TryFormat(default(Guid), default, out _, value);

            this.SetValue(ref _guidFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="NullableConverter{T,TValue}"/> when parsing nullable value types.
    /// Default is empty, which will return null for empty fields or fields that are all whitespace.
    /// </summary>
    public Utf8String Null
    {
        get => _null;
        set => this.SetValue(ref _null, value);
    }

    /// <inheritdoc/>
    internal protected override bool TryGetDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<byte>? converter)
    {
        if (EnumUtf8ConverterFactory.Instance.CanConvert(type))
        {
            converter = EnumUtf8ConverterFactory.Instance.Create(type, this);
            return true;
        }

        if (DefaultConverters.Utf8.Value.TryGetValue(type, out var factory))
        {
            converter = factory(this);
            return true;
        }

        converter = default;
        return false;
    }

    public override IHeaderBinder<byte> GetHeaderBinder() => new DefaultHeaderBinder<byte>(this);

    public override string GetAsString(ReadOnlySpan<byte> field) => Encoding.UTF8.GetString(field);

    public override ReadOnlyMemory<byte> GetNullToken(Type resultType)
    {
        if (_nullTokens is not null && _nullTokens.TryGetValue(resultType, out Utf8String value))
            return value;

        return Null;
    }

    public override bool TryWriteChars(ReadOnlySpan<char> value, Span<byte> destination, out int charsWritten)
    {
        return Encoding.UTF8.TryGetBytes(value, destination, out charsWritten);
    }

    public override bool TryGetChars(ReadOnlySpan<byte> field, Span<char> destination, out int charsWritten)
    {
        return Encoding.UTF8.TryGetChars(field, destination, out charsWritten);
    }

    public CsvConverter<byte, TValue> GetOrCreate<TValue>(Func<CsvUtf8Options, CsvConverter<byte, TValue>> func)
    {
        CsvConverter<byte>? converter = TryGetExistingOrExplicit(typeof(TValue), out bool created);

        if (converter is null)
        {
            converter = func(this);
            created = true;
        }

        if (created && !_converterCache.TryAdd(typeof(TValue), converter))
        {
            // ensure we return the same instance that was cached
            converter = (CsvConverter<byte, TValue>)_converterCache[typeof(TValue)];
        }

        return (CsvConverter<byte, TValue>)converter;
    }
}
