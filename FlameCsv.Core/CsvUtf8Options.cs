using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Binding;
using FlameCsv.Converters;
using FlameCsv.Utilities;

namespace FlameCsv;

public sealed class CsvUtf8Options : CsvOptions<byte>
{
    private static readonly Lazy<CsvUtf8Options> _default = new(() => new(isReadOnly: true));

    /// <summary>Returns a thread-safe read only singleton instance with default options.</summary>
    /// <remarks>Create a new instance if you need to configure the options or parsers.</remarks>
    public static CsvUtf8Options Default => _default.Value;

    private char _booleanFormat;
    private char _integerFormat;
    private char _decimalFormat;
    private char _dateTimeFormat;
    private char _timeSpanFormat;
    private char _guidFormat;
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
    public Utf8Char Delimiter
    {
        get => (char)_delimiter;
        set => ((ICsvDialectOptions<byte>)this).Delimiter = value;
    }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Quote"/>
    public Utf8Char Quote
    {
        get => (char)_quote;
        set => ((ICsvDialectOptions<byte>)this).Quote = value;
    }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Newline"/>
    public Utf8String Newline
    {
        get => _newline;
        set
        {
            Guard.IsNotNullOrEmpty(value, nameof(Newline));
            ((ICsvDialectOptions<byte>)this).Newline = value;
        }
    }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Whitespace"/>
    public Utf8String Whitespace
    {
        get => _newline;
        set => ((ICsvDialectOptions<byte>)this).Whitespace = value;
    }

    /// <inheritdoc cref="ICsvDialectOptions{T}.Escape"/>
    public Utf8Char? Escape
    {
        get => _escape.HasValue ? (Utf8Char)_escape.Value : null;
        set => ((ICsvDialectOptions<byte>)this).Escape = value;
    }

    public IDictionary<Type, Utf8String> NullTokens => _nullTokens ??= new TypeDictionary<Utf8String>(this);

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
            _ = Utf8Formatter.TryFormat(false, [], out _, format: value); // validate
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
        if (DefaultConverters.Utf8.Value.TryGetValue(type, out var factory))
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
        if (_nullTokens is not null && _nullTokens.TryGetValue(resultType, out Utf8String value))
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
