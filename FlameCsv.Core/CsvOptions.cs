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
using CommunityToolkit.Diagnostics;
using FlameCsv.Reading;
using CommunityToolkit.HighPerformance;

namespace FlameCsv;

internal interface IGetOrCreate<T, TSelf>
    where T : unmanaged, IEquatable<T>
    where TSelf : CsvOptions<T>
{
    CsvConverter<T, TValue> GetOrCreate<TValue>(Func<TSelf, CsvConverter<T, TValue>> func);
}

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

        _comparer = other._comparer;
        _shouldSkipRow = other._shouldSkipRow;
        _exceptionHandler = other._exceptionHandler;
        _hasHeader = other._hasHeader;
        _allowContentInExceptions = other._allowContentInExceptions;
        _useDefaultConverters = other._useDefaultConverters;
        _ignoreEnumCase = other._ignoreEnumCase;
        _allowUndefinedEnumValues = other._allowUndefinedEnumValues;
        _arrayPool = other._arrayPool;
        _stringPool = other._stringPool;
        _disableBuffering = other._disableBuffering;
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
        if (IsReadOnly)
            return false;

        ValidateDialect();
        IsReadOnly = true;
        return true;
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
    /// <seealso cref="WriteText{TWriter}(TWriter, ReadOnlySpan{char})"/>
    public abstract string GetAsString(ReadOnlySpan<T> field);

    public abstract bool TryGetChars(ReadOnlySpan<T> field, Span<char> destination, out int charsWritten);

    /// <summary>
    /// Writes the text to <paramref name="writer"/>.
    /// </summary>
    /// <param name="value">Text to write</param>
    /// <seealso cref="GetAsString(ReadOnlySpan{T})"/>
    public abstract bool TryWriteChars(ReadOnlySpan<char> value, Span<T> destination, out int charsWritten);

    /// <summary>
    /// Returns the default parsers that are used to initialize <see cref="Converters"/> in derived types.
    /// </summary>
    internal protected abstract bool TryGetDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<T>? converter);

    internal CsvRecordSkipPredicate<T>? _shouldSkipRow;
    internal CsvExceptionHandler<T>? _exceptionHandler;
    internal bool _hasHeader = true;
    internal bool _validateFieldCount;
    internal CsvFieldEscaping _fieldEscaping;
    internal ArrayPool<T> _arrayPool = ArrayPool<T>.Shared;
    internal bool _allowContentInExceptions;
    internal IList<(string text, bool value)>? _booleanValues;

    private IEqualityComparer<string> _comparer = StringComparer.OrdinalIgnoreCase;
    private bool _useDefaultConverters = true;
    private bool _ignoreEnumCase = true;
    private bool _allowUndefinedEnumValues;
    private bool _disableBuffering;
    private StringPool? _stringPool;

    /// <summary>
    /// Disables buffering newline ranges when reading. Buffering increases raw throughput,
    /// but can in some cases raise errors later in the parsing pipeline than without.
    /// </summary>
    public bool NoLineBuffering
    {
        get => _disableBuffering;
        set => this.SetValue(ref _disableBuffering, value);
    }

    /// <summary>
    /// Text comparison used to match header names.
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
    /// Whether to ensure that all records have the same number of fields. The first read or written CSV recoird
    /// is used as the source of truth for the record count, regardless of whether it was a header record or not.
    /// Object parsing always validates the field count. Default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Example: reading/writing a header with 5 fields ensures that every record read/written afterwards has exactly 5 fields.
    /// </remarks>
    public bool ValidateFieldCount
    {
        get => _validateFieldCount;
        set => this.SetValue(ref _validateFieldCount, value);
    }

    /// <summary>
    /// Defines the quoting behavior when writing values. Default is <see cref="CsvFieldEscaping.Auto"/>.
    /// </summary>
    public CsvFieldEscaping FieldEscaping
    {
        get => _fieldEscaping;
        set
        {
            GuardEx.IsDefined(value);
            this.SetValue(ref _fieldEscaping, value);
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
        get => ReferenceEquals(_arrayPool, AllocatingArrayPool<T>.Instance) ? null : _arrayPool;
        set => this.SetValue(ref _arrayPool, value ?? AllocatingArrayPool<T>.Instance);
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
    internal readonly ConcurrentDictionary<Type, CsvConverter<T>> _converterCache = new();

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
    /// <remarks>
    /// Never returns a factory.
    /// </remarks>
    /// <param name="resultType">Type to convert</param>
    /// <exception cref="CsvParserMissingException"/>
    public CsvConverter<T> GetConverter(Type resultType)
    {
        return TryGetConverter(resultType) ?? throw new CsvParserMissingException(typeof(T), resultType);
    }

    public CsvConverter<T, TResult>? TryGetConverter<TResult>()
    {
        return TryGetConverter(typeof(TResult)) as CsvConverter<T, TResult>;
    }

    /// <summary>
    /// Returns a converter for values of the parameter type, or null if there is no
    /// converter registered for <paramref name="resultType"/>.
    /// </summary>
    /// <param name="resultType">Type to convert</param>
    public CsvConverter<T>? TryGetConverter(Type resultType)
    {
        CsvConverter<T>? converter = TryGetExistingOrExplicit(resultType, out bool created);

        if (converter is null && UseDefaultConverters)
        {
            // prefer explicit nullable converter if possible
            if (TryGetDefaultConverter(resultType, out var builtin))
            {
                Debug.Assert(builtin.CanConvert(resultType));
                converter = builtin.GetOrCreateConverter(resultType, this);
                created = true;
            }
            else if (NullableConverterFactory<T>.Instance.CanConvert(resultType))
            {
                converter = NullableConverterFactory<T>.Instance.Create(resultType, this);
                created = true;
            }
        }

        if (created && converter is not null && !_converterCache.TryAdd(resultType, converter))
        {
            // ensure we return the same instance that was cached
            converter = _converterCache[resultType];
        }

        Debug.Assert(
            converter is not CsvConverterFactory<T>,
            $"TryGetConverter returned a factory: {converter?.GetType().ToTypeString()}");

        return converter;
    }

    protected internal CsvConverter<T>? TryGetExistingOrExplicit(Type resultType, out bool created)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        MakeReadOnly();

        if (_converterCache.TryGetValue(resultType, out var cached))
        {
            Debug.Assert(cached.CanConvert(resultType));
            created = false;
            return cached;
        }

        ReadOnlySpan<CsvConverter<T>> converters = _converters.Span;

        // Read converters in reverse order so parser added last has the highest priority
        for (int i = converters.Length - 1; i >= 0; i--)
        {
            if (converters[i].CanConvert(resultType))
            {
                created = true;
                return converters[i].GetOrCreateConverter(resultType, this);
            }
        }

        created = false;
        return null;
    }

    internal TValue Materialize<TValue>(ReadOnlyMemory<T> record, IMaterializer<T, TValue> materializer)
    {
        ArgumentNullException.ThrowIfNull(materializer);
        MakeReadOnly();

        T[]? array = null;

        try
        {
            var meta = CsvParser<T>.GetRecordMeta(record, this);
            CsvFieldReader<T> reader = new(
                this,
                record,
                stackalloc T[Token<T>.StackLength],
                ref array,
                in meta);

            return materializer.Parse(ref reader);
        }
        finally
        {
            _arrayPool?.EnsureReturned(ref array);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ValidateDialect()
    {
        List<string>? errors = null;

        T delimiter = _delimiter;
        T quote = _quote;
        T? escape = _escape;
        ReadOnlySpan<T> whitespace = _whitespace.Span;

        if (delimiter.Equals(quote))
        {
            AddError("Delimiter and Quote must not be equal.");
        }

        if (escape.HasValue)
        {
            if (escape.GetValueOrDefault().Equals(delimiter))
                AddError("Escape must not be equal to Delimiter.");

            if (escape.GetValueOrDefault().Equals(quote))
                AddError("Escape must not be equal to Quote.");
        }

        ReadOnlySpan<T> newline = _newline.Span;

        if (newline.Length == 0)
        {
            // use crlf if we have a known token type
            if (typeof(T) == typeof(char))
            {
                newline = "\r\n".AsSpan().UnsafeCast<char, T>();
            }
            else if (typeof(T) == typeof(byte))
            {
                newline = "\r\n"u8.UnsafeCast<byte, T>();
            }
        }

        if (newline.Length is not (1 or 2))
        {
            AddError("Newline must be empty, or 1 or 2 characters long.");
        }
        else
        {
            if (newline.Contains(delimiter))
                AddError("Newline must not contain Delimiter.");

            if (newline.Contains(quote))
                AddError("Newline must not contain Quote.");

            if (escape.HasValue && newline.Contains(escape.GetValueOrDefault()))
                AddError("Newline must not contain Escape.");
        }

        if (!whitespace.IsEmpty)
        {
            if (whitespace.Contains(delimiter))
                AddError("Whitespace must not contain Delimiter.");

            if (whitespace.Contains(quote))
                AddError("Whitespace must not contain Quote.");

            if (escape.HasValue && whitespace.Contains(escape.GetValueOrDefault()))
                AddError("Whitespace must not contain Escape.");

            if (whitespace.IndexOfAny(newline) >= 0)
                AddError("Whitespace must not contain Newline characters.");
        }

        if (errors is not null)
            Throw(errors);

        [MethodImpl(MethodImplOptions.NoInlining)]
        void AddError(string message) => (errors ??= []).Add(message);

        static void Throw(List<string> errors)
        {
            throw new CsvConfigurationException($"Invalid CsvOptions tokens: {string.Join(" ", errors)}");
        }
    }
}
