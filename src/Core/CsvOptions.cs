using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Attributes;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.IO.Internal;
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
    private static readonly CsvOptions<T> _default;

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
            throw InvalidTokenTypeEx();
        }
    }

    /// <summary>
    /// Initializes an options-instance with default configuration.
    /// </summary>
    public CsvOptions()
    {
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

        _recordCallback = other._recordCallback;
        _hasHeader = other._hasHeader;
        _validateFieldCount = other._validateFieldCount;
        _fieldQuoting = other._fieldQuoting;
        _memoryPool = other._memoryPool;
        _booleanValues = other._booleanValues;
        _formatProvider = other._formatProvider;
        _providers = other._providers?.Clone();
        _formats = other._formats?.Clone();
        _styles = other._styles?.Clone();
        _comparer = other._comparer;
        _useDefaultConverters = other._useDefaultConverters;

        _enumFormat = other._enumFormat;
        _enumFlagsSeparator = other._enumFlagsSeparator;
        _ignoreEnumCase = other._ignoreEnumCase;
        _allowUndefinedEnumValues = other._allowUndefinedEnumValues;

        _typeBinder = other._typeBinder;
        _null = other._null;
        _nullTokens = other._nullTokens is null ? null : new(this, other._nullTokens);
        _converters = other._converters?.Clone();

        _delimiter = other._delimiter;
        _quote = other._quote;
        _escape = other._escape;
        _newline = other._newline;
        _trimming = other._trimming;

        ConverterCache = new(other.ConverterCache, other.ConverterCache.Comparer);
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
        get => _typeBinder ??= new CsvReflectionBinder<T>(this, ignoreUnmatched: false); // lazy initialization
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.SetValue(ref _typeBinder, value);
        }
    }

    /// <summary>
    /// Returns the <see langword="null"/> value for parsing and formatting for the parameter type.
    /// Returns <see cref="Null"/> if none is configured in <see cref="NullTokens"/>.
    /// </summary>
    public ReadOnlyMemory<T> GetNullToken(Type resultType)
    {
        Utf8String? value = _nullTokens.TryGetExt(resultType, defaultValue: _null);

        if (value is null || value.String.Length == 0)
            return default;

        if (typeof(T) == typeof(char))
        {
            ReadOnlyMemory<char> chars = value.String.AsMemory();
            return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref chars);
        }

        if (typeof(T) == typeof(byte))
        {
            return Unsafe.As<T[]>(value.GetBytes());
        }

        throw InvalidTokenTypeEx();
    }

    internal ReadOnlySpan<T> GetNullSpan(Type resultType)
    {
        Utf8String? value = _nullTokens.TryGetExt(resultType, defaultValue: _null);

        if (value is null || value.String.Length == 0)
            return [];

        if (typeof(T) == typeof(char))
        {
            return value.String.AsSpan().Cast<char, T>();
        }

        if (typeof(T) == typeof(byte))
        {
            return Unsafe.As<T[]>(value.GetBytes());
        }

        throw InvalidTokenTypeEx();
    }

    /// <summary>
    /// Returns the custom format configured for <paramref name="resultType"/>,
    /// or <paramref name="defaultValue"/> by default.
    /// </summary>
    public string? GetFormat(Type resultType, string? defaultValue = null)
    {
        return _formats.TryGetExt(resultType, defaultValue);
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

    private CsvRecordCallback<T>? _recordCallback;
    private bool _hasHeader = true;
    private bool _validateFieldCount;
    private CsvFieldQuoting _fieldQuoting = CsvFieldQuoting.Auto;
    private MemoryPool<T> _memoryPool = MemoryPool<T>.Shared;
    private SealableList<(string, bool)>? _booleanValues;
    private bool _useDefaultConverters = true;
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
        get => _null;
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
    /// String comparer used to match CSV header to members and parameters.
    /// Default is <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    /// <remarks>
    /// The parsed CSV header fields are converted to strings using <see cref="TryGetChars"/> or
    /// <see cref="GetAsString(ReadOnlySpan{T})"/>.
    /// </remarks>
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
    /// Whether to read/write a header record. The default is <c>true</c>.
    /// </summary>
    public bool HasHeader
    {
        get => _hasHeader;
        set => this.SetValue(ref _hasHeader, value);
    }

    /// <summary>
    /// Defines the quoting behavior when writing values. Default is <see cref="CsvFieldQuoting.Auto"/>.
    /// </summary>
    /// <remarks>
    /// You can combine conditions using bitwise OR. For example, to quote both empty fields, and fields with leading spaces:
    /// <code>CsvFieldQuoting.Empty | CsvFieldQuoting.LeadingSpaces</code>
    /// </remarks>
    public CsvFieldQuoting FieldQuoting
    {
        get => _fieldQuoting;
        set => this.SetValue(ref _fieldQuoting, value);
    }

    /// <summary>
    /// If <c>true</c>, validates that all records have the same number of fields
    /// when reading <see cref="CsvRecord{T}"/> or writing with <see cref="CsvWriter{T}"/>.
    /// Default is <c>false</c>.
    /// </summary>
    public bool ValidateFieldCount
    {
        get => _validateFieldCount;
        set => this.SetValue(ref _validateFieldCount, value);
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
    /// - For token type <see langword="char"/> any type that implements <see cref="ISpanFormattable"/> and <see cref="ISpanParsable{TSelf}"/>.<br/>
    /// - For token type <see langword="byte"/> any type that implements at least one of <see cref="IUtf8SpanFormattable"/> and
    /// <see cref="IUtf8SpanParsable{TSelf}"/>, with <see cref="ISpanFormattable"/> and <see cref="ISpanParsable{TSelf}"/> as fallbacks.<br/>
    /// </remarks>
    public bool UseDefaultConverters
    {
        get => _useDefaultConverters;
        set => this.SetValue(ref _useDefaultConverters, value);
    }

    /// <summary>
    /// Pool used to create reusable buffers when reading multisegment data, or unescaping large fields.
    /// Default value is <see cref="MemoryPool{T}.Shared"/>.
    /// Set to <see langword="null"/> to disable pooling and always heap allocate.
    /// </summary>
    /// <remarks>
    /// Buffers that are larger than <see cref="MemoryPool{T}.MaxBufferSize"/> size are rented from the shared array pool.
    /// </remarks>
    public MemoryPool<T>? MemoryPool
    {
        get => ReferenceEquals(_memoryPool, HeapMemoryPool<T>.Instance) ? null : _memoryPool;
        set => this.SetValue(ref _memoryPool, value ?? HeapMemoryPool<T>.Instance);
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
    internal MemoryPool<T> Allocator => _memoryPool;
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
