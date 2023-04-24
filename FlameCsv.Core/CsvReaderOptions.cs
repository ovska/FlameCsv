using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Parsers;
using FlameCsv.Utilities;
using static FlameCsv.Utilities.SealableUtil;

namespace FlameCsv;

/// <summary>
/// Represents a base class for configuration used to read and parse CSV data.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public abstract partial class CsvReaderOptions<T> : ISealable
    where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Initializes an options-instance with default options and no parsers defined.
    /// </summary>
    protected CsvReaderOptions()
    {
    }

    protected CsvReaderOptions(CsvReaderOptions<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _stringComparison = other._stringComparison;
        _shouldSkipRow = other._shouldSkipRow;
        _exceptionHandler = other._exceptionHandler;
        _hasHeader = other._hasHeader;
        _allowContentInExceptions = other._allowContentInExceptions;
        _arrayPool = other._arrayPool;

        // copy collections
        _parsers = new(this, other.Parsers);
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
        return !IsReadOnly && MakeReadOnlyCore(this);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool MakeReadOnlyCore(CsvReaderOptions<T> _this)
        {
            lock (_this._parserCache)
            {
                if (!_this.IsReadOnly)
                {
                    _this.GetOrInitParsers();
                    return _this.IsReadOnly = true;
                }
            }

            return false;
        }
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
    /// Returns the value used to match to <see langword="null"/> for the parameter type.
    /// </summary>
    public abstract ReadOnlyMemory<T> GetNullToken(Type resultType);

    /// <summary>
    /// Returns a <see langword="string"/> representation of the value.
    /// </summary>
    public abstract string GetAsString(ReadOnlySpan<T> field);

    /// <summary>
    /// Overridden values that match to null when parsing <see cref="Nullable{T}"/> instead of the default, <see cref="Null"/>.
    /// </summary>
    /// <remarks>
    /// Modifying the collection after the options instance is used (<see cref="IsReadOnly"/> is <see langword="true"/>)
    /// results in an exception.
    /// </remarks>
    public abstract ITypeMap<string?> NullTokens { get; }

    private StringComparison _stringComparison = StringComparison.OrdinalIgnoreCase;
    private CsvCallback<T, bool>? _shouldSkipRow;
    private CsvExceptionHandler<T>? _exceptionHandler;
    private bool _hasHeader;
    private bool _allowContentInExceptions;
    private ArrayPool<T>? _arrayPool = ArrayPool<T>.Shared;

    /// <summary>
    /// Text comparison used to match header names.
    /// </summary>
    public StringComparison Comparison
    {
        get => _stringComparison;
        set
        {
            "".Equals("", comparisonType: value);
            this.SetValue(ref _stringComparison, value);
        }
    }

    /// <summary>
    /// Delegate that determines whether a row should be skipped.
    /// Default is <see langword="null"/>, which means all rows are processed.
    /// </summary>
    public CsvCallback<T, bool>? ShouldSkipRow
    {
        get => _shouldSkipRow;
        set => this.SetValue(ref _shouldSkipRow, value);
    }

    /// <summary>
    /// Delegate that is called when an exception is thrown while parsing values. If null (the default), or the
    /// delegate returns false, the exception is considered unhandled and is thrown.<para/>For example, to ignore
    /// unparseable values return <see langword="true"/> if the exception is <see cref="CsvParseException"/>. In
    /// this case, rows with invalid data are skipped, see also: <see cref="ShouldSkipRow"/>.
    /// </summary>
    public CsvExceptionHandler<T>? ExceptionHandler
    {
        get => _exceptionHandler;
        set => this.SetValue(ref _exceptionHandler, value);
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
    /// Whether the read CSV has a header record.
    /// </summary>
    /// <seealso cref="HeaderBinder"/>
    public bool HasHeader
    {
        get => _hasHeader;
        set => this.SetValue(ref _hasHeader, value);
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
    /// Collection of all parsers and factories of the options instance.
    /// </summary>
    /// <remarks>
    /// Modifying the collection after the options instance is used (<see cref="IsReadOnly"/> is <see langword="true"/>)
    /// results in an exception.
    /// </remarks>
    public IList<ICsvParser<T>> Parsers => _parsers ?? GetOrInitParsers();

    private SealableList<ICsvParser<T>>? _parsers;
    private readonly ConcurrentDictionary<Type, ICsvParser<T>> _parserCache = new();

    /// <summary>
    /// Returns the default parsers that are used to initialize <see cref="Parsers"/> in derived types.
    /// </summary>
    protected virtual IEnumerable<ICsvParser<T>> GetDefaultParsers() => Enumerable.Empty<ICsvParser<T>>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    [MemberNotNull(nameof(_parsers))]
    private SealableList<ICsvParser<T>> GetOrInitParsers()
    {
        if (_parsers is not null)
            return _parsers;

        var parserList = new SealableList<ICsvParser<T>>(this, GetDefaultParsers());
        return Interlocked.CompareExchange(ref _parsers, parserList, null) ?? parserList;
    }

    /// <summary>
    /// Returns a parser for parsing <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">Type to parse</typeparam>
    /// <exception cref="CsvParserMissingException"/>
    public ICsvParser<T, TResult> GetParser<TResult>()
    {
        return (ICsvParser<T, TResult>)GetParser(typeof(TResult));
    }

    /// <summary>
    /// Returns a parser for parsing values of the parameter type.
    /// </summary>
    /// <param name="resultType">Type to parse</param>
    /// <exception cref="CsvParserMissingException"/>
    public ICsvParser<T> GetParser(Type resultType)
    {
        return TryGetParser(resultType) ?? throw new CsvParserMissingException(typeof(T), resultType);
    }

    public ICsvParser<T, TResult>? TryGetParser<TResult>() => (ICsvParser<T, TResult>?)TryGetParser(typeof(TResult));

    /// <summary>
    /// Returns a parser for parsing values of the parameter type, or null if there is no
    /// parser registered for <paramref name="resultType"/>.
    /// </summary>
    /// <param name="resultType">Type to parse</param>
    public ICsvParser<T>? TryGetParser(Type resultType)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        MakeReadOnly();

        if (_parserCache.TryGetValue(resultType, out var cached))
        {
            Debug.Assert(cached.CanParse(resultType));
            return cached;
        }

        ReadOnlySpan<ICsvParser<T>> parsers = (_parsers ?? GetOrInitParsers()).Span;

        // Read parsers in reverse order so parser added last has the highest priority
        for (int i = parsers.Length - 1; i >= 0; i--)
        {
            if (parsers[i].CanParse(resultType))
            {
                var parser = parsers[i].GetParserOrFromFactory(resultType, this);
                _parserCache.TryAdd(resultType, parser);
                return parser;
            }
        }

        return null;
    }

    internal ReadOnlySpan<ICsvParser<T>> EnumerateParsers()
    {
        MakeReadOnly();
        return (_parsers ?? GetOrInitParsers()).Span;
    }
}
