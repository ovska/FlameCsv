using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Converters.Enums;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Object used to configure dialect, converters, and other options to read and write CSV data.
/// </summary>
/// <typeparam name="T">Token type (<c>char</c> or <c>byte</c>)</typeparam>
[PublicAPI]
public sealed partial class CsvOptions<T> : ICanBeReadOnly
    where T : unmanaged, IBinaryInteger<T>
{
    internal static readonly CsvOptions<T> _default;

    static CsvOptions()
    {
        if (typeof(T) == typeof(char) || typeof(T) == typeof(byte))
        {
            _default = new CsvOptions<T>();
            _default.MakeReadOnly();
        }
        else
        {
            _default = null!;
        }
    }

    /// <summary>
    /// Returns read-only default options for <typeparamref name="T"/> with the same configuration as <c>new()</c>.
    /// The returned instance is thread-safe.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="NotSupportedException"/> for token types other than <see langword="char"/> or <see langword="byte"/>.
    /// </remarks>
    public static CsvOptions<T> Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (typeof(T) == typeof(char) || typeof(T) == typeof(byte))
                return _default;
            throw Token<T>.NotSupported;
        }
    }

    /// <summary>
    /// Initializes an options-instance with default configuration.
    /// </summary>
    public CsvOptions()
    {
        if (typeof(T) != typeof(char) && typeof(T) != typeof(byte))
        {
            throw Token<T>.NotSupported;
        }

        HotReloadService.RegisterForHotReload(
            this,
            static state =>
            {
                var @this = (CsvOptions<T>)state;
                @this.ConverterCache.Clear();
            }
        );
    }

    /// <summary>
    /// Initializes a new instance of options, copying the configuration from <paramref name="other"/>.
    /// </summary>
    public CsvOptions(CsvOptions<T> other)
        : this()
    {
        ArgumentNullException.ThrowIfNull(other);

        _config = other._config;

        _recordCallback = other._recordCallback;
        _fieldQuoting = other._fieldQuoting;
        _booleanValues = other._booleanValues;
        _formatProvider = other._formatProvider;
        _providers = other._providers?.Clone();
        _formats = other._formats?.Clone();
        _styles = other._styles?.Clone();
        _comparer = other._comparer;

        _enumFormat = other._enumFormat;
        _enumFlagsSeparator = other._enumFlagsSeparator;

        _typeBinder = other._typeBinder;
        _null = other._null;
        _nullTokens = other._nullTokens is null ? null : new(this, other._nullTokens);
        _converters = other._converters?.Clone();

        _delimiter = other._delimiter;
        _quote = other._quote;
        _newline = other._newline;
        _trimming = other._trimming;

        // converter cache should NOT be copied, as it might contain converters for types
        // that are configured for this options later, but due to the cache never used
    }

    /// <summary>
    /// Whether the options-instance is sealed and can no longer be modified.
    /// Options become read only after they begin being used to avoid concurrency bugs.<br/>
    /// Create a copy of the instance if you need to modify it after this point.
    /// </summary>
    /// <seealso cref="CsvOptions{T}(CsvOptions{T})"/>
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// Seals the instance from modifications.
    /// </summary>
    /// <remarks>
    /// Automatically called by the framework when the instance is used.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MakeReadOnly()
    {
        if (!IsReadOnly)
        {
            Validate();
            IsReadOnly = true;
        }
    }

    /// <summary>
    /// The type binder used for reflection code paths.
    /// The default value is <see cref="CsvReflectionBinder{T}"/>.
    /// </summary>
    /// <seealso cref="CsvTypeMap{T,TValue}"/>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}"/>
    public ICsvTypeBinder<T> TypeBinder
    {
        get => _typeBinder ??= new CsvReflectionBinder<T>(this);
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.SetValue(ref _typeBinder, value);
        }
    }

    internal ReadOnlySpan<T> DefaultNullToken
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _null is null ? [] : _null.AsSpan<T>();
    }

    /// <summary>
    /// Returns the <see langword="null"/> value for parsing and formatting for the parameter type.
    /// Returns <see cref="Null"/> if none is configured in <see cref="NullTokens"/>.
    /// </summary>
    public ReadOnlyMemory<T> GetNullToken(Type resultType)
    {
        Utf8String? value = _nullTokens.TryGetExt(resultType, defaultValue: _null);
        return value is null ? ReadOnlyMemory<T>.Empty : value.AsMemory<T>();
    }

    internal ReadOnlySpan<T> GetNullSpan(Type resultType)
    {
        Utf8String? value = _nullTokens.TryGetExt(resultType, defaultValue: _null);
        return value is null ? [] : value.AsSpan<T>();
    }

    /// <summary>
    /// Returns the custom format configured for <paramref name="resultType"/>,
    /// or <paramref name="defaultValue"/> by default.
    /// </summary>
    public string? GetFormat(Type resultType, string? defaultValue = null)
    {
        return _formats.TryGetExt(resultType, defaultValue);
    }

    internal bool HasCustomFormat(Type resultType)
    {
        return _formats?.ContainsKey(resultType) == true;
    }

    /// <summary>
    /// Returns the custom format provider configured for <paramref name="resultType"/>,
    /// or <see cref="FormatProvider"/> by default.
    /// </summary>
    public IFormatProvider? GetFormatProvider(Type resultType)
    {
        return _providers.TryGetExt(resultType, _formatProvider);
    }

    /// <summary>
    /// Returns the custom number styles configured for the <see cref="INumberBase{TSelf}"/>,
    /// or <paramref name="defaultValue"/> by default.
    /// </summary>
    /// <remarks>
    /// Defaults are <see cref="NumberStyles.Integer"/> for <see cref="IBinaryInteger{TSelf}"/> and
    /// <see cref="NumberStyles.Float"/> for <see cref="IFloatingPoint{TSelf}"/>.
    /// </remarks>
    public NumberStyles GetNumberStyles(Type resultType, NumberStyles defaultValue)
    {
        return _styles.TryGetExt(resultType, defaultValue);
    }

    private Config _config = Config.HasHeader | Config.UseDefaultConverters | Config.IgnoreEnumCase;

    private CsvRecordCallback<T>? _recordCallback;
    private CsvExceptionHandler<T>? _exceptionHandler;

    private CsvFieldQuoting _fieldQuoting = CsvFieldQuoting.Auto;
    private SealableList<(string, bool)>? _booleanValues;
    private ICsvTypeBinder<T>? _typeBinder;

    private IEqualityComparer<string> _comparer = StringComparer.OrdinalIgnoreCase;

    private Utf8String? _null;
    private TypeStringDictionary? _nullTokens;
    private TypeDictionary<string?>? _formats;
    private TypeDictionary<NumberStyles>? _styles;

    private IFormatProvider? _formatProvider = CultureInfo.InvariantCulture;
    private TypeDictionary<IFormatProvider?>? _providers;

    /// <summary>
    /// Default null token to use when writing null values or reading nullable structs.
    /// Default is <see langword="null"/> (empty field in CSV).
    /// </summary>
    /// <seealso cref="NullTokens"/>
    public string? Null
    {
        get => _null?.String;
        set
        {
            this.ThrowIfReadOnly();
            _null = value is null ? null : new Utf8String(value);
        }
    }

    /// <summary>
    /// Format provider used it none is defined for type in <see cref="FormatProviders"/>.
    /// Defaults to <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    /// <seealso cref="FormatProviders"/>
    /// <seealso cref="GetFormatProvider(Type)"/>
    public IFormatProvider? FormatProvider
    {
        get => _formatProvider;
        set => this.SetValue(ref _formatProvider, value);
    }

    /// <summary>
    /// Format providers used per type.
    /// </summary>
    /// <remarks>Structs and their <see cref="Nullable{T}"/> counterparts are treated as equal.</remarks>
    /// <seealso cref="FormatProvider"/>
    /// <seealso cref="GetFormatProvider(Type)"/>
    public IDictionary<Type, IFormatProvider?> FormatProviders
    {
        get => _providers ??= new TypeDictionary<IFormatProvider?>(this);
    }

    /// <summary>
    /// Format used per type.
    /// </summary>
    /// <remarks>Structs and their <see cref="Nullable{T}"/> counterparts are treated as equal.</remarks>
    /// <seealso cref="GetFormat(Type, string?)"/>
    public IDictionary<Type, string?> Formats
    {
        get => _formats ??= new TypeDictionary<string?>(this);
    }

    /// <summary>
    /// Styles used when parsing <see cref="IBinaryNumber{TSelf}"/> and <see cref="IFloatingPoint{TSelf}"/>.
    /// </summary>
    /// <remarks>Structs and their <see cref="Nullable{T}"/> counterparts are treated as equal.</remarks>
    /// <seealso cref="GetNumberStyles(Type, System.Globalization.NumberStyles)"/>.
    public IDictionary<Type, NumberStyles> NumberStyles
    {
        get => _styles ??= new TypeDictionary<NumberStyles>(this);
    }

    /// <summary>
    /// String comparer used to match CSV headers and <see cref="BooleanValues"/>.
    /// Default is <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    public IEqualityComparer<string> Comparer
    {
        get => _comparer;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.SetValue(ref _comparer, value);
        }
    }

    /// <summary>
    /// Delegate that is called for each record before it is processed.
    /// Can be used to skip records or reset the header.
    /// </summary>
    /// <remarks>
    /// All records are expected to be valid CSV, so even if the callback skips a record (e.g. starting with <c>#</c>),
    /// it must still have valid CSV structure.
    /// </remarks>
    public CsvRecordCallback<T>? RecordCallback
    {
        get => _recordCallback;
        set => this.SetValue(ref _recordCallback, value);
    }

    /// <summary>
    /// Delegate that is called when an exception is thrown when parsing a record.<br/>
    /// Not used with `Enumerate()` or `AsParallel()`.
    /// </summary>
    public CsvExceptionHandler<T>? ExceptionHandler
    {
        get => _exceptionHandler;
        set => this.SetValue(ref _exceptionHandler, value);
    }

    /// <summary>
    /// Whether to read/write a header record. The default is <c>true</c>.
    /// </summary>
    public bool HasHeader
    {
        get => _config.GetFlag(Config.HasHeader);
        set
        {
            this.ThrowIfReadOnly();
            _config.SetFlag(Config.HasHeader, value);
        }
    }

    /// <summary>
    /// Defines the quoting behavior when writing values. Default is <see cref="CsvFieldQuoting.Auto"/>.
    /// </summary>
    /// <remarks>
    /// You can combine conditions using bitwise OR. For example, to quote both empty fields and fields with leading spaces:
    /// <c>CsvFieldQuoting.Empty | CsvFieldQuoting.LeadingSpaces</c>
    /// </remarks>
    public CsvFieldQuoting FieldQuoting
    {
        get => _fieldQuoting;
        set => this.SetValue(ref _fieldQuoting, value);
    }

    /// <summary>
    /// Whether to use the library's built-in converters. Default is <c>true</c>. The converters can be configured
    /// using properties of <see cref="CsvOptions{T}"/>.
    /// </summary>
    /// <remarks>
    /// The following types are supported by default:<br/>
    /// - <see langword="string"/><br/>
    /// - <see langword="enum"/><br/>
    /// - <see langword="bool"/><br/>
    /// - Common <see cref="IBinaryNumber{TSelf}"/> types such as <see langword="int"/> and <see langword="long"/>)<br/>
    /// - Common <see cref="IFloatingPoint{TSelf}"/> types such as <see langword="double"/> and <see langword="float"/><br/>
    /// - <see cref="Guid"/><br/>
    /// - <see cref="DateTime"/><br/>
    /// - <see cref="DateTimeOffset"/><br/>
    /// - <see cref="TimeSpan"/><br/>
    /// - For UTF16 (<see langword="char"/>) any type that implements <see cref="ISpanFormattable"/> and <see cref="ISpanParsable{TSelf}"/>.<br/>
    /// - For UTF8/ASCII (<see langword="byte"/>) any type that implements <see cref="IUtf8SpanFormattable"/> or <see cref="ISpanFormattable"/>, and
    /// <see cref="IUtf8SpanParsable{TSelf}"/> or <see cref="ISpanParsable{TSelf}"/><br/>
    /// </remarks>
    public bool UseDefaultConverters
    {
        get => _config.GetFlag(Config.UseDefaultConverters);
        set
        {
            this.ThrowIfReadOnly();
            _config.SetFlag(Config.UseDefaultConverters, value);
        }
    }

    /// <summary>
    /// Whether to ignore headers that cannot be matched to any property, field, or constructor parameter.
    /// Default is <c>false</c>, which throws an exception if an unrecognized header field is encountered.
    /// </summary>
    public bool IgnoreUnmatchedHeaders
    {
        get => _config.GetFlag(Config.IgnoreUnmatchedHeaders);
        set
        {
            this.ThrowIfReadOnly();
            _config.SetFlag(Config.IgnoreUnmatchedHeaders, value);
        }
    }

    /// <summary>
    /// Whether to ignore CSV headers that match to the same property, field, or constructor parameter
    /// as another header. If <c>true</c>, only the first member that matches a header is used.
    /// Default is <c>false</c>, which throws an exception if a duplicate header is encountered.
    /// </summary>
    public bool IgnoreDuplicateHeaders
    {
        get => _config.GetFlag(Config.IgnoreDuplicateHeaders);
        set
        {
            this.ThrowIfReadOnly();
            _config.SetFlag(Config.IgnoreDuplicateHeaders, value);
        }
    }

    /// <summary>
    /// Returns tokens used to parse and format <see langword="null"/> values. See <see cref="GetNullToken(Type)"/>.
    /// </summary>
    /// <seealso cref="CsvConverter{T,TValue}.CanFormatNull"/>
    public IDictionary<Type, string?> NullTokens
    {
        get => _nullTokens ??= new(this);
    }

    /// <summary>
    /// Optional custom boolean value mapping. If not empty, must contain at least one value for both
    /// <c>true</c> and <c>false</c>. Default is empty.
    /// </summary>
    /// <seealso cref="UseDefaultConverters"/>
    /// <seealso cref="CsvBooleanValuesAttribute"/>
    public IList<(string text, bool value)> BooleanValues
    {
        get => _booleanValues ??= (IsReadOnly ? SealableList<(string, bool)>.Empty : new(this, null));
    }

    internal bool HasBooleanValues => _booleanValues is { Count: > 0 };

    [Flags]
    private enum Config : byte
    {
        Default = 0,
        HasHeader = 1 << 0,
        UseDefaultConverters = 1 << 1,
        IgnoreEnumCase = 1 << 2,
        AllowUndefinedEnums = 1 << 3,
        IgnoreUnmatchedHeaders = 1 << 4,
        IgnoreDuplicateHeaders = 1 << 5,
    }
}

file static class TypeDictExtensions
{
    [StackTraceHidden]
    public static T TryGetExt<T>(
        this TypeDictionary<T>? dict,
        Type key,
        T defaultValue,
        [CallerArgumentExpression(nameof(key))] string parameterName = ""
    )
    {
        ArgumentNullException.ThrowIfNull(key, parameterName);
        return dict is not null && dict.TryGetValue(key, out T? value) ? value : defaultValue;
    }
}
