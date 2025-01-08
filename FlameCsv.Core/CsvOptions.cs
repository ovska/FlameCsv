using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using CommunityToolkit.HighPerformance.Buffers;
using System.Globalization;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Object used to configure dialect, converters and other options to read and write CSV data.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public partial class CsvOptions<T> : ICanBeReadOnly where T : unmanaged, IBinaryInteger<T>
{
    private static readonly Lazy<CsvOptions<T>> _defaultOptions = new(
        () =>
        {
            if (typeof(T) != typeof(char) && typeof(T) != typeof(byte))
                ThrowInvalidTokenType(nameof(Default));

            var options = new CsvOptions<T>();
            options.MakeReadOnly();
            return options;
        });

    /// <summary>
    /// Returns read-only default options for <typeparamref name="T"/>, with same configuration as <see langword="new"/>().
    /// </summary>
    /// <remarks>
    /// Throws <see cref="NotSupportedException"/> for token types other than <see langword="char"/> or <see langword="byte"/>.
    /// </remarks>
    public static CsvOptions<T> Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _defaultOptions.Value;
    }

    /// <summary>
    /// Initializes an options-instance with default configuration.
    /// </summary>
    public CsvOptions()
    {
        _converters = new SealableList<CsvConverter<T>>(this, defaultValues: null);

#if DEBUG
        _allowContentInExceptions = true;
#endif
    }

    public CsvOptions(CsvOptions<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _shouldSkipRow = other._shouldSkipRow;
        _exceptionHandler = other._exceptionHandler;
        _hasHeader = other._hasHeader;
        _validateFieldCount = other._validateFieldCount;
        _fieldEscaping = other._fieldEscaping;
        _memoryPool = other._memoryPool;
        _allowContentInExceptions = other._allowContentInExceptions;
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
        _converters = new(this, other._converters); // copy collection
    }

    /// <summary>
    /// Whether the options instance is sealed and can no longer be modified.
    /// Options become read only after they begin being used to avoid concurrency bugs.
    /// </summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// Seals the instance from modifications.
    /// </summary>
    /// <returns><see langword="true"/> if the instance was made readonly, <see langword="false"/> if it already was.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MakeReadOnly()
    {
        if (!IsReadOnly)
        {
            InitializeDialect();

            // set to readonly only after dialect has been validated
            IsReadOnly = true;
        }
    }

    /// <summary>
    /// Returns the header binder that matches CSV header record fields to parsed type's properties/fields.
    /// See also <see cref="Comparer"/>.
    /// </summary>
    /// <remarks>
    /// By default, CSV header is matched to property/field names and
    /// <see cref="Binding.Attributes.CsvHeaderAttribute"/>.
    /// </remarks>
    public virtual IHeaderBinder<T> GetHeaderBinder() => new DefaultHeaderBinder<T>(this);

    /// <summary>
    /// Returns the <see langword="null"/> value for parsing and formatting for the parameter type.
    /// Returns <see cref="Null"/> if none is configured in <see cref="NullTokens"/>.
    /// </summary>
    public virtual ReadOnlyMemory<T> GetNullToken(Type resultType)
    {
        if (typeof(T) != typeof(char) && typeof(T) != typeof(byte))
        {
            ThrowInvalidTokenType(nameof(GetNullToken));
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
            Debug.Assert(_null is Tuple<Utf8String>, $"Invalid null type for {typeof(T)}: {_null.GetType()}");
            var value = (ReadOnlyMemory<byte>)Unsafe.As<Tuple<Utf8String>>(_null).Item1;
            return Unsafe.As<ReadOnlyMemory<byte>, ReadOnlyMemory<T>>(ref value);
        }

        throw new UnreachableException();
    }

    /// <summary>
    /// Returns the format configured for <paramref name="resultType"/>, or <paramref name="defaultValue"/> by default.
    /// </summary>
    public virtual string? GetFormat(Type resultType, string? defaultValue = null)
        => _formats.GetOrDefault(resultType, defaultValue);

    /// <summary>
    /// Returns the format provider configured for <paramref name="resultType"/>, or <see cref="FormatProvider"/> by default.
    /// </summary>
    public virtual IFormatProvider? GetFormatProvider(Type resultType)
        => _providers.GetOrDefault(resultType, _formatProvider);

    /// <summary>
    /// Returns the number styles configured for the <see cref="INumberBase{TSelf}"/>, or <paramref name="defaultValue"/> by default.
    /// </summary>
    /// <remarks>
    /// Defaults are <see cref="NumberStyles.Integer"/> for <see cref="IBinaryInteger{TSelf}"/> and
    /// <see cref="NumberStyles.Float"/> for <see cref="IFloatingPoint{TSelf}"/>.
    /// </remarks>
    public virtual NumberStyles GetNumberStyles(Type resultType, NumberStyles defaultValue)
        => _styles.GetOrDefault(resultType, defaultValue);

    internal CsvRecordSkipPredicate<T>? _shouldSkipRow;
    internal CsvExceptionHandler<T>? _exceptionHandler;
    internal bool _hasHeader = true;
    internal bool _validateFieldCount;
    internal CsvFieldEscaping _fieldEscaping;
    internal MemoryPool<T> _memoryPool = MemoryPool<T>.Shared;
    private bool _allowContentInExceptions;
    internal SealableList<(string, bool)>? _booleanValues;
    private bool _useDefaultConverters = true;
    private bool _ignoreEnumCase = true;
    private string? _enumFormat;
    private bool _allowUndefinedEnumValues;
    private bool _disableBuffering;
    private StringPool? _stringPool;

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

            ThrowInvalidTokenType(nameof(Null));
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

            ThrowInvalidTokenType(nameof(Null));
        }
    }

    /// <summary>
    /// Format provider used it none is defined for type in <see cref="FormatProviders"/>.
    /// Defaults to <see cref="CultureInfo.InvariantCulture"/>. See <see cref="GetFormatProvider(Type)"/>.
    /// </summary>
    public IFormatProvider? FormatProvider
    {
        get => _formatProvider;
        set => this.SetValue(ref _formatProvider, value);
    }

    /// <summary>
    /// Format provider user per type instead of <see cref="FormatProvider"/>. See <see cref="GetFormatProvider(Type)"/>.
    /// </summary>
    public ITypeDictionary<IFormatProvider?> FormatProviders
        => _providers ??= new TypeDictionary<IFormatProvider?, object>(this);

    /// <summary>
    /// Format used per type. See <see cref="GetFormat(Type, string?)"/>.
    /// </summary>
    public ITypeDictionary<string?> Formats => _formats ??= new TypeDictionary<string?, object>(this);

    /// <summary>
    /// Parsing styles used per <see cref="IBinaryNumber{TSelf}"/> and <see cref="IFloatingPoint{TSelf}"/>.
    /// See <see cref="GetNumberStyles(Type, System.Globalization.NumberStyles)"/>.
    /// </summary>
    public ITypeDictionary<NumberStyles> NumberStyles => _styles ??= new TypeDictionary<NumberStyles, object>(this);

    /// <summary>
    /// Disables buffering newline ranges when reading.
    /// </summary>
    /// <remarks>
    /// Buffering increases reading performance, but lines are buffered without parsing their individual fields,
    /// which may cause extra data to be read when one of the fields contains an erroneus value.
    /// </remarks>
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
    /// delegate returns false, the exception is considered unhandled and is thrown.<para/>For example, to ignore
    /// unparseable values return <see langword="true"/> if the exception is <see cref="CsvParseException"/>. In
    /// this case, rows with invalid data are skipped, see also: <see cref="ShouldSkipRow"/>.
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
    /// Pooling reduces raw throughput, but can have profound impact on allocations if the data has a lot of repeating strings.
    /// </remarks>
    /// <seealso cref="Binding.Attributes.CsvStringPoolingAttribute{T}"/>
    public StringPool? StringPool
    {
        get => _stringPool;
        set => this.SetValue(ref _stringPool, value);
    }

    /// <summary>
    /// If <see langword="true"/>, CSV content is included in exception messages. Default is
    /// <see langword="false"/>, which will only show the CSV structure relative to delimiters/quotes/newlines.
    /// </summary>
    public bool AllowContentInExceptions
    {
        get => _allowContentInExceptions;
        set => this.SetValue(ref _allowContentInExceptions, value);
    }

    /// <summary>
    /// Whether to read/write a header record. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, types must be annotated with <see cref="Binding.Attributes.CsvIndexAttribute"/>.
    /// </remarks>
    public bool HasHeader
    {
        get => _hasHeader;
        set => this.SetValue(ref _hasHeader, value);
    }

    /// <summary>
    /// Defines the quoting behavior when writing values. Default is <see cref="CsvFieldEscaping.Auto"/>.
    /// </summary>
    public CsvFieldEscaping FieldEscaping
    {
        get => _fieldEscaping;
        set
        {
            if (!Enum.IsDefined(value))
                Throw.Argument(nameof(value), "Value not defined in enum CsvFieldEscaping");

            this.SetValue(ref _fieldEscaping, value);
        }
    }

    /// <summary>
    /// If <see langword="true"/> validates that all records have the same amount of fields when reading or writing CSV.
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
    /// Whether to ignore case when parsing enum values. Default is <see langword="true"/>.
    /// </summary>
    public bool IgnoreEnumCase
    {
        get => _ignoreEnumCase;
        set => this.SetValue(ref _ignoreEnumCase, value);
    }

    /// <summary>
    /// Whether to allow enum values that are not defined in the enum type.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool AllowUndefinedEnumValues
    {
        get => _allowUndefinedEnumValues;
        set => this.SetValue(ref _allowUndefinedEnumValues, value);
    }

    /// <summary>
    /// The default format for enums, used if enum's format is not defined in <see cref="Formats"/>.
    /// </summary>
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
    /// Default allocator uses <see cref="MemoryPool{T}.Shared"/>.
    /// Set to <see langword="null"/> to disable pooling and always heap allocate.
    /// </summary>
    public MemoryPool<T>? MemoryPool
    {
        get => ReferenceEquals(_memoryPool, HeapMemoryPool<T>.Shared) ? null : _memoryPool;
        set => this.SetValue(ref _memoryPool, value ?? HeapMemoryPool<T>.Shared);
    }

    /// <summary>
    /// Returns tokens used to parse and format <see langword="null"/> values. See <see cref="GetNullToken(Type)"/>.
    /// </summary>
    /// <seealso cref="CsvConverter{T,TValue}.HandleNull"/>
    public ITypeDictionary<string?> NullTokens => _nullTokens ??= new(this, static Utf8String (str) => str);

    /// <summary>
    /// Optional custom boolean value mapping. If not empty, must contain at least one value for both
    /// <see langword="true"/> and <see langword="false"/>. Default is empty.
    /// </summary>
    /// <seealso cref="Binding.Attributes.CsvBooleanTextValuesAttribute"/>
    /// <seealso cref="Binding.Attributes.CsvBooleanUtf8ValuesAttribute"/>
    public IList<(string text, bool value)> BooleanValues
        => _booleanValues ??= new SealableList<(string, bool)>(this, null);

    private bool TryGetExistingOrCustomConverter(
        Type resultType,
        [NotNullWhen(true)] out CsvConverter<T>? converter,
        out bool created)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        MakeReadOnly();

        if (_converterCache.TryGetValue(resultType, out var cached))
        {
            Debug.Assert(cached.CanConvert(resultType));
            converter = cached;
            created = false;
            return true;
        }

        ReadOnlySpan<CsvConverter<T>> converters = _converters.Span;

        // Read converters in reverse order so parser added last has the highest priority
        for (int i = converters.Length - 1; i >= 0; i--)
        {
            if (converters[i].CanConvert(resultType))
            {
                converter = converters[i].GetOrCreateConverter(resultType, this);
                created = true;
                return true;
            }
        }

        converter = null;
        created = false;
        return false;
    }
}

file static class TypeDictExtensions
{
    [StackTraceHidden]
    public static T GetOrDefault<T>(
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
