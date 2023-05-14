using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Utilities;

namespace FlameCsv;

/// <summary>
/// A configurable reader options with common built-in parsers.
/// </summary>
/// <remarks>
/// Initialized with the following parsers:
/// <list type="bullet">
/// <item><see cref="StringUtf8Converter"/></item>
/// <item><see cref="IntegerUtf8Parser"/></item>
/// <item><see cref="BooleanUtf8Parser"/></item>
/// <item><see cref="DateTimeUtf8Parser"/></item>
/// <item><see cref="DecimalUtf8Parser"/></item>
/// <item><see cref="EnumUtf8ConverterFactory"/></item>
/// <item><see cref="NullableConverterFactory{T}"/></item>
/// <item><see cref="GuidUtf8Converter"/></item>
/// <item><see cref="TimeSpanUtf8Parser"/></item>
/// <item><see cref="Base64Utf8Parser"/></item>
/// </list>
/// </remarks>
public sealed class CsvUtf8Options : CsvOptions<byte>
{
    private static readonly Lazy<CsvUtf8Options> _default = new(() => new(isReadOnly: true));

    /// <summary>Returns a thread-safe read only singleton instance with default options.</summary>
    /// <remarks>Create a new instance if you need to configure the options or parsers.</remarks>
    public static CsvUtf8Options Default => _default.Value;

    private StringPool? _stringPool;
    private char _booleanFormat;
    private char _integerFormat;
    private char _decimalFormat;
    private char _dateTimeFormat;
    private char _timeSpanFormat;
    private char _guidFormat;
    private bool _ignoreEnumCase;
    private bool _allowUndefinedEnumValues;
    private ReadOnlyMemory<byte> _null;
    private IReadOnlyCollection<(ReadOnlyMemory<byte> bytes, bool value)>? _booleanValues;
    private TypeByteDictionary? _nullTokens;

    /// <inheritdoc cref="CsvTextOptions"/>
    public CsvUtf8Options() : this(false)
    {
    }

    public CsvUtf8Options(CsvUtf8Options other) : base(other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _stringPool = other._stringPool;
        _booleanFormat = other._booleanFormat;
        _integerFormat = other._integerFormat;
        _decimalFormat = other._decimalFormat;
        _dateTimeFormat = other._dateTimeFormat;
        _timeSpanFormat = other._timeSpanFormat;
        _guidFormat = other._guidFormat;
        _ignoreEnumCase = other._ignoreEnumCase;
        _allowUndefinedEnumValues = other._allowUndefinedEnumValues;
        _null = other._null;

        // copy collections
        _booleanValues = other._booleanValues?.ToList();
        _nullTokens = new(this, other._nullTokens);
    }

    private CsvUtf8Options(bool isReadOnly)
    {
        _delimiter = (byte)',';
        _quote = (byte)'"';
        _newline = CsvDialectStatic._crlf;

        if (isReadOnly)
            MakeReadOnly();
    }

    public CsvUtf8Options Clone() => new(this);

    public char Delimiter
    {
        get => (char)_delimiter;
        set => ((ICsvDialectOptions<byte>)this).Delimiter = CsvDialectStatic.AsByte(value);
    }

    public char Quote
    {
        get => (char)_quote;
        set => ((ICsvDialectOptions<byte>)this).Quote = CsvDialectStatic.AsByte(value);
    }

    public string Newline
    {
        get => CsvDialectStatic.AsString(_newline);
        set
        {
            Guard.IsNotNullOrEmpty(value, nameof(Newline));
            ((ICsvDialectOptions<byte>)this).Newline = CsvDialectStatic.AsBytes(value);
        }
    }

    public char? Escape
    {
        get => _escape.HasValue ? (char)_escape.Value : null;
        set => ((ICsvDialectOptions<byte>)this).Escape = value.HasValue ? CsvDialectStatic.AsByte(value.Value) : null;
    }

    public override IDictionary<Type, string?> NullTokens => _nullTokens ??= new(this);

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
    /// Used by <see cref="BooleanUtf8Converter"/> when writing booleans.
    /// Default is <c>default(char)</c>, which writes capitalized values.
    /// </summary>
    /// <remarks>
    /// Ignored if <see cref="BooleanValues"/> is not empty.
    /// </remarks>
    public char BooleanFormat
    {
        get => _booleanFormat;
        set
        {
            _ = Utf8Formatter.TryFormat(false, Span<byte>.Empty, out _, format: value); // validate
            this.SetValue(ref _booleanFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="IntegerUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char IntegerFormat
    {
        get => _integerFormat;
        set
        {
            _ = Utf8Parser.TryParse(default, out int _, out _, value); // validate
            this.SetValue(ref _integerFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="DecimalUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char DecimalFormat
    {
        get => _decimalFormat;
        set
        {
            _ = Utf8Parser.TryParse(default, out double _, out _, value); // validate
            this.SetValue(ref _decimalFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="DateTimeUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char DateTimeFormat
    {
        get => _dateTimeFormat;
        set
        {
            _ = Utf8Parser.TryParse(default, out DateTime _, out _, value); // validate
            this.SetValue(ref _dateTimeFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="TimeSpanUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char TimeSpanFormat
    {
        get => _timeSpanFormat;
        set
        {
            _ = Utf8Parser.TryParse(default, out TimeSpan _, out _, value); // validate
            this.SetValue(ref _timeSpanFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="GuidUtf8Converter"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char GuidFormat
    {
        get => _guidFormat;
        set
        {
            _ = Utf8Parser.TryParse(default, out Guid _, out _, value); // validate
            this.SetValue(ref _guidFormat, value);
        }
    }

    /// <summary>
    /// Used by <see cref="EnumUtf8Converter{TEnum}"/>. Default is <see langword="true"/>.
    /// </summary>
    public bool IgnoreEnumCase
    {
        get => _ignoreEnumCase;
        set => this.SetValue(ref _ignoreEnumCase, value);
    }

    /// <summary>
    /// Used by <see cref="EnumUtf8Converter{TEnum}"/> to optionally skip validating that the parsed value is defined.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool AllowUndefinedEnumValues
    {
        get => _allowUndefinedEnumValues;
        set => this.SetValue(ref _allowUndefinedEnumValues, value);
    }

    /// <summary>
    /// Used by <see cref="NullableConverter{T,TValue}"/> when parsing nullable value types.
    /// Default is empty, which will return null for empty fields or fields that are all whitespace.
    /// </summary>
    public ReadOnlyMemory<byte> Null
    {
        get => _null;
        set => this.SetValue(ref _null, value);
    }

    /// <summary>
    /// Optional custom boolean value mapping. Empty and null are equivalent.
    /// Default is <see langword="null"/>, which defers parsing to <see cref="System.Buffers.Text.Utf8Parser"/>.
    /// </summary>
    public IReadOnlyCollection<(ReadOnlyMemory<byte> bytes, bool value)>? BooleanValues
    {
        get => _booleanValues;
        set => this.SetValue(ref _booleanValues, value);
    }

    /// <inheritdoc/>
    internal protected override bool TryGetDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<byte>? converter)
    {
        if (DefaultConverters.Utf8.TryGetValue(type, out var factory))
        {
            converter = factory(this);
            return true;
        }

        if (EnumUtf8ConverterFactory.Instance.CanConvert(type))
        {
            converter = EnumUtf8ConverterFactory.Instance.Create(type, this);
            return true;
        }

        converter = default;
        return false;
    }

    public override IHeaderBinder<byte> GetHeaderBinder() => new DefaultHeaderBinder<byte>(this);

    public override string GetAsString(ReadOnlySpan<byte> field) => Encoding.UTF8.GetString(field);

    public override ReadOnlyMemory<byte> GetNullToken(Type resultType)
    {
        if (_nullTokens is not null && _nullTokens.TryGetInternalValue(resultType, out ReadOnlyMemory<byte> value))
            return value;

        return Null;
    }

    public override void WriteChars<TWriter>(TWriter writer, ReadOnlySpan<char> value)
    {
        if (!value.IsEmpty)
        {
            Span<byte> destination = writer.GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length));
            int bytesWritten = Encoding.UTF8.GetBytes(value, destination);
            writer.Advance(bytesWritten);
        }
    }
}
