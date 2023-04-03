using FlameCsv.Parsers;
using FlameCsv.Parsers.Utf8;

namespace FlameCsv;

/// <summary>
/// A configurable reader options with common built-in parsers.
/// </summary>
/// <remarks>
/// Initialized with the following parsers:
/// <list type="bullet">
/// <item><see cref="StringUtf8Parser"/></item>
/// <item><see cref="IntegerUtf8Parser"/></item>
/// <item><see cref="BooleanUtf8Parser"/></item>
/// <item><see cref="DateTimeUtf8Parser"/></item>
/// <item><see cref="DecimalUtf8Parser"/></item>
/// <item><see cref="EnumUtf8ParserFactory"/></item>
/// <item><see cref="NullableParserFactory{T}"/></item>
/// <item><see cref="GuidUtf8Parser"/></item>
/// <item><see cref="TimeSpanUtf8Parser"/></item>
/// <item><see cref="Base64Utf8Parser"/></item>
/// </list>
/// </remarks>
public sealed class CsvUtf8ReaderOptions :
    CsvReaderOptions<byte>,
    ICsvNullTokenProvider<byte>
{
    private static readonly Lazy<CsvUtf8ReaderOptions> _default = new(() => new(isReadOnly: true));

    /// <summary>Returns a thread-safe read only singleton instance with default options.</summary>
    /// <remarks>Create a new instance if you need to configure the options or parsers.</remarks>
    public static CsvUtf8ReaderOptions Default => _default.Value;

    private char _integerFormat;
    private char _decimalFormat;
    private char _dateTimeFormat;
    private char _timeSpanFormat;
    private char _guidFormat;
    private bool _ignoreEnumCase;
    private bool _allowUndefinedEnumValues;
    private ReadOnlyMemory<byte> _null;
    private IReadOnlyCollection<(ReadOnlyMemory<byte> bytes, bool value)>? _booleanValues;
    private Dictionary<Type, ReadOnlyMemory<byte>>? _nullOverrides;

    /// <inheritdoc cref="CsvTextReaderOptions"/>
    public CsvUtf8ReaderOptions() : this(false)
    {
    }

    private CsvUtf8ReaderOptions(bool isReadOnly)
    {
        _delimiter = (byte)',';
        _quote = (byte)'"';
        _newline = CsvDialectStatic._crlf;
        _whitespace = ReadOnlyMemory<byte>.Empty;

        if (isReadOnly)
            MakeReadOnly();
    }

    /// <summary>
    /// Used by <see cref="IntegerUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char IntegerFormat
    {
        get => _integerFormat;
        set => SetValue(ref _integerFormat, value);
    }

    /// <summary>
    /// Used by <see cref="DecimalUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char DecimalFormat
    {
        get => _decimalFormat;
        set => SetValue(ref _decimalFormat, value);
    }

    /// <summary>
    /// Used by <see cref="DateTimeUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char DateTimeFormat
    {
        get => _dateTimeFormat;
        set => SetValue(ref _dateTimeFormat, value);
    }

    /// <summary>
    /// Used by <see cref="TimeSpanUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char TimeSpanFormat
    {
        get => _timeSpanFormat;
        set => SetValue(ref _timeSpanFormat, value);
    }

    /// <summary>
    /// Used by <see cref="GuidUtf8Parser"/>. Default is <c>default(char)</c>.
    /// </summary>
    public char GuidFormat
    {
        get => _guidFormat;
        set => SetValue(ref _guidFormat, value);
    }

    /// <summary>
    /// Used by <see cref="EnumUtf8Parser{TEnum}"/>. Default is <see langword="true"/>.
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
    /// Used by <see cref="NullableParser{T,TValue}"/> when parsing nullable value types.
    /// Default is empty, which will return null for empty columns or columns that are all whitespace.
    /// </summary>
    public ReadOnlyMemory<byte> Null
    {
        get => _null;
        set => SetValue(ref _null, value);
    }

    /// <summary>
    /// Optional custom boolean value mapping. Empty and null are equivalent.
    /// Default is <see langword="null"/>, which defers parsing to <see cref="System.Buffers.Text.Utf8Parser"/>.
    /// </summary>
    public IReadOnlyCollection<(ReadOnlyMemory<byte> bytes, bool value)>? BooleanValues
    {
        get => _booleanValues;
        set => SetValue(ref _booleanValues, value);
    }

    /// <summary>
    /// Overridden values that match to null when parsing <see cref="Nullable{T}"/>
    /// instead of the default <see cref="Null"/>.
    /// </summary>
    public IDictionary<Type, ReadOnlyMemory<byte>> NullOverrides => _nullOverrides ??= new();

    /// <inheritdoc/>
    protected override IEnumerable<ICsvParser<byte>> GetDefaultParsers()
    {
        return new ICsvParser<byte>[]
        {
            Base64Utf8Parser.Instance,
            new TimeSpanUtf8Parser(TimeSpanFormat),
            new GuidUtf8Parser(GuidFormat),
            new EnumUtf8ParserFactory(AllowUndefinedEnumValues, IgnoreEnumCase),
            new NullableParserFactory<byte>(Null),
            new DateTimeUtf8Parser(DateTimeFormat),
            new DecimalUtf8Parser(DecimalFormat),
            new BooleanUtf8Parser(BooleanValues),
            new IntegerUtf8Parser(IntegerFormat),
            StringUtf8Parser.Instance,
        };
    }

    ReadOnlyMemory<byte> ICsvNullTokenProvider<byte>.Default => Null;

    bool ICsvNullTokenProvider<byte>.TryGetOverride(Type type, out ReadOnlyMemory<byte> value)
    {
        if (_nullOverrides is not null)
            return _nullOverrides.TryGetValue(type, out value);

        value = default;
        return false;
    }
}
