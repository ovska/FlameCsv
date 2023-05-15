using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Converters;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using static FlameCsv.Utilities.SealableUtil;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv;

/// <summary>
/// Represents a base class for configuration used to read and write CSV data.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public abstract partial class CsvOptions<T> : ISealable where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Initializes an options-instance with default configuration.
    /// </summary>
    protected CsvOptions()
    {
        _converters = new SealableList<CsvConverter<T>>(this, defaultValues: null);

#if DEBUG
        _allowContentInExceptions = true;
#endif
    }

    protected CsvOptions(CsvOptions<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _stringComparison = other._stringComparison;
        _shouldSkipRow = other._shouldSkipRow;
        _exceptionHandler = other._exceptionHandler;
        _hasHeader = other._hasHeader;
        _allowContentInExceptions = other._allowContentInExceptions;
        _useDefaultConverters = other._useDefaultConverters;
        _ignoreEnumCase = other._ignoreEnumCase;
        _allowUndefinedEnumValues = other._allowUndefinedEnumValues;
        _arrayPool = other._arrayPool;
        _stringPool = other._stringPool;
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
    public bool MakeReadOnly()
    {
        return !IsReadOnly && (IsReadOnly = true);
    }

    /// <summary>
    /// Returns the header binder that matches CSV header record fields to parsed type's properties/fields.
    /// </summary>
    /// <remarks>
    /// By default, CSV header is matched to property/field names and
    /// <see cref="Binding.Attributes.CsvHeaderAttribute"/> using <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </remarks>
    public abstract IHeaderBinder<T> GetHeaderBinder();

    /// <summary>
    /// Returns the <see langword="null"/> value for parsing and formatting for the parameter type.
    /// </summary>
    public abstract ReadOnlyMemory<T> GetNullToken(Type resultType);

    /// <summary>
    /// Returns a <see langword="string"/> representation of the value.
    /// </summary>
    public abstract string GetAsString(ReadOnlySpan<T> field);

    /// <summary>
    /// Writes the characters to the token writer.
    /// </summary>
    /// <typeparam name="TWriter">Writer to write to</typeparam>
    /// <param name="writer">Buffer writer instance</param>
    /// <param name="value">Value to write</param>
    public abstract void WriteChars<TWriter>(TWriter writer, ReadOnlySpan<char> value) where TWriter : IBufferWriter<T>;

    /// <summary>
    /// Returns the default parsers that are used to initialize <see cref="Converters"/> in derived types.
    /// </summary>
    internal protected abstract bool TryGetDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<T>? converter);

    private IEqualityComparer<string> _stringComparison = StringComparer.OrdinalIgnoreCase;
    private RowSkipCallback<T>? _shouldSkipRow;
    private CsvExceptionHandler<T>? _exceptionHandler;
    private bool _hasHeader = true;
    private bool _validateFieldCount;
    private CsvFieldQuoting _fieldQuoting;
    private bool _allowContentInExceptions;
    private bool _useDefaultConverters = true;
    private bool _ignoreEnumCase = true;
    private bool _allowUndefinedEnumValues;
    private ArrayPool<T>? _arrayPool = ArrayPool<T>.Shared;
    private StringPool? _stringPool;
    internal IList<(string text, bool value)>? _booleanValues;

    /// <summary>
    /// Text comparison used to match header names.
    /// </summary>
    public IEqualityComparer<string> Comparer
    {
        get => _stringComparison;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.SetValue(ref _stringComparison, value);
        }
    }

    /// <summary>
    /// Delegate that determines whether a row should be skipped.
    /// Default is <see langword="null"/>, which means all rows are processed.
    /// </summary>
    public RowSkipCallback<T>? ShouldSkipRow
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
    /// <see cref="CsvFormatException"/> is not handled, as it represents an invalid CSV.<br/>
    /// This handler is not used in the enumerators that return <see cref="CsvValueRecord{T}"/>, you can catch
    /// exceptions thrown manually when handling the record.
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
    /// Whether the read CSV has a header record. The default is <see langword="true"/>.
    /// </summary>
    /// <seealso cref="HeaderBinder"/>
    public bool HasHeader
    {
        get => _hasHeader;
        set => this.SetValue(ref _hasHeader, value);
    }

    /// <summary>
    /// Whether to ensure that all records have the same number of fields. The first non-skipped row of the CSV
    /// is used as the source of truth for the record count, regardless of whether it was a header record or not.
    /// Object parsing always validates the field count. Default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Causes <see cref="CsvValueRecord{T}"/> instances to eagerly read the whole record when they are initialized.
    /// </remarks>
    public bool ValidateFieldCount
    {
        get => _validateFieldCount;
        set => this.SetValue(ref _validateFieldCount, value);
    }

    /// <summary>
    /// Defines the quoting behavior when writing values. Default is <see cref="CsvFieldQuoting.Auto"/>.
    /// </summary>
    public CsvFieldQuoting FieldQuoting
    {
        get => _fieldQuoting;
        set
        {
            GuardEx.IsDefined(value);
            this.SetValue(ref _fieldQuoting, value);
        }
    }

    /// <summary>
    /// Whether to use the library's built-in converters. Default is <see langword="true"/>.
    /// </summary>
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
    /// Pool used to create reusable buffers when needed. Default is <see cref="ArrayPool{T}.Shared"/>.
    /// Set to <see langword="null"/> to disable pooling and always allocate.
    /// </summary>
    public ArrayPool<T>? ArrayPool
    {
        get => _arrayPool;
        set => this.SetValue(ref _arrayPool, value);
    }

    /// <summary>
    /// Optional custom boolean value mapping. If not empty, must contain at least one value for both
    /// <see langword="true"/> and <see langword="false"/>. Default is empty.
    /// </summary>
    public IList<(string text, bool value)> BooleanValues
    {
        get => _booleanValues ??= new List<(string, bool)>();
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.SetValue(ref _booleanValues, value);
        }
    }

    /// <summary>
    /// Collection of all converters and factories of the options instance.
    /// </summary>
    /// <remarks>
    /// Modifying the collection after the options instance is used (<see cref="IsReadOnly"/> is <see langword="true"/>)
    /// results in an exception.
    /// </remarks>
    public IList<CsvConverter<T>> Converters => _converters;

    private readonly SealableList<CsvConverter<T>> _converters;
    private readonly ConcurrentDictionary<Type, CsvConverter<T>> _converterCache = new();

    /// <summary>
    /// Returns a converter for <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">Type to convert</typeparam>
    /// <exception cref="CsvParserMissingException"/>
    public CsvConverter<T, TResult> GetConverter<TResult>()
    {
        return (CsvConverter<T, TResult>)GetConverter(typeof(TResult));
    }

    /// <summary>
    /// Returns a converter for values of the parameter type.
    /// </summary>
    /// <param name="resultType">Type to convert</param>
    /// <exception cref="CsvParserMissingException"/>
    public CsvConverter<T> GetConverter(Type resultType)
    {
        return TryGetConverter(resultType) ?? throw new CsvParserMissingException(typeof(T), resultType);
    }

    public CsvConverter<T, TResult>? TryGetConverter<TResult>() => (CsvConverter<T, TResult>?)TryGetConverter(typeof(TResult));

    /// <summary>
    /// Returns a converter for values of the parameter type, or null if there is no
    /// converter registered for <paramref name="resultType"/>.
    /// </summary>
    /// <param name="resultType">Type to convert</param>
    public CsvConverter<T>? TryGetConverter(Type resultType)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        MakeReadOnly();

        if (_converterCache.TryGetValue(resultType, out var cached))
        {
            Debug.Assert(cached.CanConvert(resultType));
            return cached;
        }

        CsvConverter<T>? converter = TryGetConverterCore(resultType);

        if (converter is not null)
        {
            _converterCache.TryAdd(resultType, converter);
        }

        return converter;
    }

    internal protected CsvConverter<T>? TryGetConverterCore(Type resultType)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        MakeReadOnly();

        ReadOnlySpan<CsvConverter<T>> converters = _converters.Span;

        // Read parsers in reverse order so parser added last has the highest priority
        for (int i = converters.Length - 1; i >= 0; i--)
        {
            if (converters[i].CanConvert(resultType))
            {
                return converters[i].GetParserOrFromFactory(resultType, this);
            }
        }

        if (UseDefaultConverters)
        {
            if (TryGetDefaultConverter(resultType, out var builtin))
            {
                Debug.Assert(builtin.CanConvert(resultType));
                return builtin.GetParserOrFromFactory(resultType, this);
            }
            else if (NullableConverterFactory<T>.Instance.CanConvert(resultType))
            {
                return NullableConverterFactory<T>.Instance.Create(resultType, this);
            }
        }

        return null;
    }

    internal ReadOnlySpan<CsvConverter<T>> EnumerateConverters()
    {
        MakeReadOnly();
        return _converters.Span;
    }
}
