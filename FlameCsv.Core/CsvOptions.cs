using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using CommunityToolkit.HighPerformance.Buffers;
using System.Globalization;
using JetBrains.Annotations;
using System.Runtime.CompilerServices;
using FlameCsv.Binding.Attributes;
#if DEBUG
using Unsafe = FlameCsv.Extensions.DebugUnsafe
#else
using Unsafe = System.Runtime.CompilerServices.Unsafe
#endif
    ;

namespace FlameCsv;

/// <summary>
/// Object used to configure dialect, converters, and other options to read and write CSV data.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public partial class CsvOptions<T> : ICanBeReadOnly where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns read-only default options for <typeparamref name="T"/> with same configuration as <c>new()</c>.
    /// </summary>
    /// <remarks>
    /// Throws <see cref="NotSupportedException"/> for token types other than <see langword="char"/> or <see langword="byte"/>.
    /// </remarks>
    public static CsvOptions<T> Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (typeof(T) == typeof(char)) return Unsafe.As<CsvOptions<T>>(CsvOptionsCharSealed.Instance);
            if (typeof(T) == typeof(byte)) return Unsafe.As<CsvOptions<T>>(CsvOptionsByteSealed.Instance);
            InvalidTokenTypeEx(nameof(Default));
            return null!; // unreachable
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
                @this._converterCache.Clear();
                @this._explicitCache.Clear();
            });
    }

    /// <summary>
    /// Initializes a new instance of options, copying the configuration from <paramref name="other"/>.
    /// </summary>
    public CsvOptions(CsvOptions<T> other) : this()
    {
        ArgumentNullException.ThrowIfNull(other);

        _shouldSkipRow = other._shouldSkipRow;
        _exceptionHandler = other._exceptionHandler;
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
        _ignoreEnumCase = other._ignoreEnumCase;
        _enumFormat = other._enumFormat;
        _allowUndefinedEnumValues = other._allowUndefinedEnumValues;
        _disableBuffering = other._disableBuffering;
        _stringPool = other._stringPool;
        _null = other._null;
        _nullTokens = other._nullTokens?.Clone();
        _converters = other._converters?.Clone();

        _delimiter = other._delimiter;
        _quote = other._quote;
        _escape = other._escape;
        _newline = other._newline;
        _whitespace = other._whitespace;

        _converterCache = new(other._converterCache, other._converterCache.Comparer);
        _explicitCache = new(other._explicitCache, other._explicitCache.Comparer);

        // either of these types can be a derived type with a different max size
        CheckConverterCacheSize();
        CheckExplicitCacheSize();
    }

    /// <summary>
    /// Whether the options-instance is sealed and can no longer be modified.
    /// Options become read only after they begin being used to avoid concurrency bugs.
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
            InitializeDialect();

            // set to readonly only after the dialect has been validated
            IsReadOnly = true;
        }
    }

    /// <summary>
    /// Returns the instance that binds CSV fields to members and vice versa using reflection.
    /// The default value is <see cref="CsvReflectionBinder{T}"/>.
    /// </summary>
    /// <seealso cref="CsvTypeMap{T,TValue}"/>
    public virtual ICsvTypeBinder<T> TypeBinder
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
    public virtual ReadOnlyMemory<T> GetNullToken(Type resultType)
    {
        if (typeof(T) != typeof(char) && typeof(T) != typeof(byte))
        {
            InvalidTokenTypeEx(nameof(GetNullToken));
        }

        TypeDictionary<string?, Utf8String>? nullTokens = _nullTokens;

        if (nullTokens is not null)
        {
            if (typeof(T) == typeof(char))
            {
                if (nullTokens.TryGetValue(resultType, out string? value))
                {
                    var result = value.AsMemory();
                    return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref result);
                }
            }

            if (typeof(T) == typeof(byte))
            {
                if (nullTokens.TryGetAlternate(resultType, out Utf8String? value))
                {
                    ReadOnlyMemory<byte> result = value;
                    return Unsafe.As<ReadOnlyMemory<byte>, ReadOnlyMemory<T>>(ref result);
                }
            }
        }

        if (_null is null)
            return ReadOnlyMemory<T>.Empty;

        if (typeof(T) == typeof(char))
        {
            Debug.Assert(_null is string, $"Invalid null type for {typeof(T)}: {_null.GetType()}");
            var value = Unsafe.As<string?>(_null).AsMemory();
            return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref value);
        }

        if (typeof(T) == typeof(byte))
        {
            Debug.Assert(_null is Utf8String, $"Invalid null type for {typeof(T)}: {_null.GetType()}");
            var value = (ReadOnlyMemory<byte>)(Utf8String)_null;
            return Unsafe.As<ReadOnlyMemory<byte>, ReadOnlyMemory<T>>(ref value);
        }

        throw new UnreachableException();
    }

    /// <summary>
    /// Returns the custom format configured for <paramref name="resultType"/>,
    /// or <paramref name="defaultValue"/> by default.
    /// </summary>
    public virtual string? GetFormat(Type resultType, string? defaultValue = null)
        => _formats.TryGetExt(resultType, defaultValue);

    /// <summary>
    /// Returns the custom format provider configured for <paramref name="resultType"/>,
    /// or <see cref="FormatProvider"/> by default.
    /// </summary>
    public virtual IFormatProvider? GetFormatProvider(Type resultType)
        => _providers.TryGetExt(resultType, _formatProvider);

    /// <summary>
    /// Returns the custom number styles configured for the <see cref="INumberBase{TSelf}"/>,
    /// or <paramref name="defaultValue"/> by default.
    /// </summary>
    /// <remarks>
    /// Defaults are <see cref="NumberStyles.Integer"/> for <see cref="IBinaryInteger{TSelf}"/> and
    /// <see cref="NumberStyles.Float"/> for <see cref="IFloatingPoint{TSelf}"/>.
    /// </remarks>
    public virtual NumberStyles GetNumberStyles(Type resultType, NumberStyles defaultValue)
        => _styles.TryGetExt(resultType, defaultValue);

    internal CsvRecordSkipPredicate<T>? _shouldSkipRow;
    internal CsvExceptionHandler<T>? _exceptionHandler;
    internal bool _hasHeader = true;
    internal bool _validateFieldCount;
    internal CsvFieldQuoting _fieldQuoting;
    internal MemoryPool<T> _memoryPool = MemoryPool<T>.Shared;
    internal SealableList<(string, bool)>? _booleanValues;
    private bool _useDefaultConverters = true;
    private bool _ignoreEnumCase = true;
    private string? _enumFormat;
    private bool _allowUndefinedEnumValues;
    private bool _disableBuffering;
    private StringPool? _stringPool;
    private ICsvTypeBinder<T>? _typeBinder;

    private IEqualityComparer<string> _comparer = StringComparer.OrdinalIgnoreCase;

    private object? _null;
    private TypeDictionary<string?, Utf8String>? _nullTokens;
    private TypeDictionary<string?, object>? _formats;
    private TypeDictionary<NumberStyles, object>? _styles;

    private IFormatProvider? _formatProvider = CultureInfo.InvariantCulture;
    private TypeDictionary<IFormatProvider?, object>? _providers;

    /// <summary>
    /// Default null token to use when writing null values or reading nullable structs.
    /// Default is <see langword="null"/> (empty field in CSV).
    /// </summary>
    /// <seealso cref="NullTokens"/>
    public virtual string? Null
    {
        get
        {
            if (typeof(T) == typeof(char))
            {
                return (string?)_null;
            }

            if (typeof(T) == typeof(byte))
            {
                return (Utf8String?)_null;
            }

            InvalidTokenTypeEx(nameof(Null));
            return null;
        }
        set
        {
            this.ThrowIfReadOnly();

            if (value is null || typeof(T) == typeof(char))
            {
                _null = value;
                return;
            }

            if (typeof(T) == typeof(byte))
            {
                _null = (Utf8String?)value;
                return;
            }

            InvalidTokenTypeEx(nameof(Null));
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
        => _providers ??= new TypeDictionary<IFormatProvider?, object>(this);

    /// <summary>
    /// Format used per type.
    /// </summary>
    /// <remarks>Structs and their <see cref="Nullable{T}"/> counterparts are treated as equal.</remarks>
    /// <seealso cref="GetFormat(Type, string?)"/>
    public IDictionary<Type, string?> Formats => _formats ??= new TypeDictionary<string?, object>(this);

    /// <summary>
    /// Styles used when parsing <see cref="IBinaryNumber{TSelf}"/> and <see cref="IFloatingPoint{TSelf}"/>.
    /// </summary>
    /// <remarks>Structs and their <see cref="Nullable{T}"/> counterparts are treated as equal.</remarks>
    /// <seealso cref="GetNumberStyles(Type, System.Globalization.NumberStyles)"/>.
    public IDictionary<Type, NumberStyles> NumberStyles => _styles ??= new TypeDictionary<NumberStyles, object>(this);

    /// <summary>
    /// Disables buffering CSV fields to memory when reading.
    /// Default is <see langword="false"/>.
    /// If set to <see langword="true"/>, performance is degraded but records will always be read one at a time.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public bool NoLineBuffering
    {
        get => _disableBuffering;
        set => this.SetValue(ref _disableBuffering, value);
    }

    /// <summary>
    /// String comparer used to match CSV header to members and parameters.
    /// Default is <see cref="StringComparer.OrdinalIgnoreCase"/>.
    /// </summary>
    /// <remarks>
    /// Header names are converted to strings using <see cref="GetAsString(ReadOnlySpan{T})"/>.
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
    /// Delegate that determines whether a row should be skipped.
    /// Default is <see langword="null"/>, which means all rows are processed.
    /// </summary>
    public CsvRecordSkipPredicate<T>? ShouldSkipRow
    {
        get => _shouldSkipRow;
        set => this.SetValue(ref _shouldSkipRow, value);
    }

    /// <summary>
    /// Delegate that is called when an exception is thrown while parsing class records. If null (the default), or the
    /// delegate returns false, the exception is considered unhandled and is thrown.<para/>
    /// For example, to ignore unparseable values return <see langword="true"/> if the exception is
    /// <see cref="CsvParseException"/>. In this case, rows with invalid data are skipped, see also:
    /// <see cref="ShouldSkipRow"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="CsvFormatException"/> is always thrown as it represents an invalid CSV.<br/>
    /// This handler is not used in the enumerators that return <see cref="CsvValueRecord{T}"/>, you can catch
    /// exceptions thrown manually when reading fields from the record.
    /// </remarks>
    public CsvExceptionHandler<T>? ExceptionHandler
    {
        get => _exceptionHandler;
        set => this.SetValue(ref _exceptionHandler, value);
    }

    /// <summary>
    /// String pool to use when parsing strings. Default is <see langword="null"/>, which results in no pooling.
    /// </summary>
    /// <remarks>
    /// Pooling reduces raw throughput, but can have a profound impact on allocations
    /// if the data has a lot of repeating strings.
    /// </remarks>
    /// <seealso cref="Binding.Attributes.CsvStringPoolingAttribute{T}"/>
    public StringPool? StringPool
    {
        get => _stringPool;
        set => this.SetValue(ref _stringPool, value);
    }

    /// <summary>
    /// Whether to read/write a header record. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, types must be annotated with <see cref="Binding.Attributes.CsvIndexAttribute"/>
    /// unless a custom <see cref="TypeBinder"/> is used.
    /// </remarks>
    public bool HasHeader
    {
        get => _hasHeader;
        set => this.SetValue(ref _hasHeader, value);
    }

    /// <summary>
    /// Defines the quoting behavior when writing values. Default is <see cref="CsvFieldQuoting.Auto"/>.
    /// </summary>
    public CsvFieldQuoting FieldQuoting
    {
        get => _fieldQuoting;
        set
        {
            if (!Enum.IsDefined(value))
                Throw.Argument(nameof(value), "Value not defined in enum CsvFieldEscaping");

            this.SetValue(ref _fieldQuoting, value);
        }
    }

    /// <summary>
    /// If <see langword="true"/> validates that all records have the same number of fields when reading or writing CSV.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool ValidateFieldCount
    {
        get => _validateFieldCount;
        set => this.SetValue(ref _validateFieldCount, value);
    }

    /// <summary>
    /// Whether to use the library's built-in converters. Default is <see langword="true"/>. The converters can be configured
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
    /// Whether to ignore casing when parsing enum values. Default is <see langword="true"/>.
    /// </summary>
    /// <seealso cref="UseDefaultConverters"/>
    public bool IgnoreEnumCase
    {
        get => _ignoreEnumCase;
        set => this.SetValue(ref _ignoreEnumCase, value);
    }

    /// <summary>
    /// Whether to allow enum values that are not defined in the enum type.
    /// Default is <see langword="false"/>.
    /// </summary>
    /// <seealso cref="UseDefaultConverters"/>
    public bool AllowUndefinedEnumValues
    {
        get => _allowUndefinedEnumValues;
        set => this.SetValue(ref _allowUndefinedEnumValues, value);
    }

    /// <summary>
    /// The default format for enums, used if enum's format is not defined in <see cref="Formats"/>.
    /// </summary>
    /// <seealso cref="UseDefaultConverters"/>
    public string? EnumFormat
    {
        get => _enumFormat;
        set
        {
            // validate
            _ = Enum.TryFormat(default(CsvBindingScope), default, out _, value);
            this.SetValue(ref _enumFormat, value);
        }
    }

    /// <summary>
    /// Pool used to create reusable buffers when reading multisegment data, or unescaping large fields.
    /// Default value is <see cref="MemoryPool{T}.Shared"/>.
    /// Set to <see langword="null"/> to disable pooling and always heap allocate.
    /// </summary>
    public MemoryPool<T>? MemoryPool
    {
        get => ReferenceEquals(_memoryPool, HeapMemoryPool<T>.Instance) ? null : _memoryPool;
        set => this.SetValue(ref _memoryPool, value ?? HeapMemoryPool<T>.Instance);
    }

    /// <summary>
    /// Returns tokens used to parse and format <see langword="null"/> values. See <see cref="GetNullToken(Type)"/>.
    /// </summary>
    /// <seealso cref="CsvConverter{T,TValue}.CanFormatNull"/>
    public IDictionary<Type, string?> NullTokens => _nullTokens ??= new(this, static Utf8String (str) => str);

    /// <summary>
    /// Optional custom boolean value mapping. If not empty, must contain at least one value for both
    /// <see langword="true"/> and <see langword="false"/>. Default is empty.
    /// </summary>
    /// <seealso cref="UseDefaultConverters"/>
    /// <seealso cref="CsvBooleanValuesAttribute{T}"/>
    public IList<(string text, bool value)> BooleanValues
        => _booleanValues ??= new SealableList<(string, bool)>(this, null);
}

file static class TypeDictExtensions
{
    [StackTraceHidden]
    public static T TryGetExt<T>(
        this TypeDictionary<T, object>? dict,
        Type key,
        T defaultValue,
        [CallerArgumentExpression(nameof(key))]
        string parameterName = "")
    {
        ArgumentNullException.ThrowIfNull(key, parameterName);
        return dict is not null && dict.TryGetValue(key, out T? value) ? value : defaultValue;
    }
}

file sealed class CsvOptionsCharSealed : CsvOptions<char>
{
    public static readonly CsvOptionsCharSealed Instance;

    static CsvOptionsCharSealed()
    {
        Instance = new();
        Instance.MakeReadOnly();
    }
}

file sealed class CsvOptionsByteSealed : CsvOptions<byte>
{
    public static readonly CsvOptionsByteSealed Instance;

    static CsvOptionsByteSealed()
    {
        Instance = new();
        Instance.MakeReadOnly();
    }
}
