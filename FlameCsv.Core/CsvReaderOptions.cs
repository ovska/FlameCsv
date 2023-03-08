using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Parsers;

namespace FlameCsv;

/// <summary>
/// Represents a base class for configuration used to read and parse CSV data.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public partial class CsvReaderOptions<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Initializes an options-instance with default options and no parsers defined.
    /// </summary>
    public CsvReaderOptions()
    {
    }

    /// <summary>
    /// Whether the options instance is sealed and can no longer be modified.
    /// </summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// Seals the instance from modifications.
    /// </summary>
    /// <returns><see langword="true"/> if the instance was made readonly, <see langword="false"/> if it already was.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool MakeReadOnly()
    {
        if (!IsReadOnly)
        {
            lock (_parserCache)
            {
                if (!IsReadOnly)
                {
                    _ = GetOrInitParsers();
                    IsReadOnly = true;
                    return true;
                }
            }
        }

        return false;
    }

    internal CsvTokens<T> tokens = CsvTokens<T>.GetDefaultForOptions();
    private CsvCallback<T, bool>? _shouldSkipRow;
    private CsvExceptionHandler<T>? _exceptionHandler;
    private bool _hasHeader;
    private SecurityLevel _securityLevel = SecurityLevel.NoBufferClearing;
    private IHeaderBinder<T>? _headerBinder;
    private ArrayPool<T>? _arrayPool = ArrayPool<T>.Shared;

    /// <summary>
    /// Tokens used for CSV parsing. Defaults to <see cref="CsvTokens{T}.Windows"/> on supported types of
    /// <typeparamref name="T"/> and to uninitialized on unsupported types.
    /// </summary>
    /// <seealso cref="CsvTokens{T}.Windows"/>
    /// <exception cref="CsvConfigurationException">Thrown invalid options are set.</exception>
    public CsvTokens<T> Tokens
    {
        get => tokens;
        set => SetValue(ref tokens, value.ThrowIfInvalid());
    }

    /// <summary>
    /// Delegate that determines whether a row should be skipped.
    /// Default is <see langword="null"/>, which means all rows are processed.
    /// </summary>
    public CsvCallback<T, bool>? ShouldSkipRow
    {
        get => _shouldSkipRow;
        set => SetValue(ref _shouldSkipRow, value);
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
        set => SetValue(ref _exceptionHandler, value);
    }

    /// <summary>
    /// Flags determining if the CSV data can be exposed outside FlameCSV and the code running it.
    /// Default is <see cref="SecurityLevel.NoBufferClearing"/>, which allows pooled memory
    /// to be returned without clearing them.
    /// </summary>
    // TODO: actually propagate this everywhere
    public SecurityLevel Security
    {
        get => _securityLevel;
        set => SetValue(ref _securityLevel, value);
    }

    /// <summary>
    /// Whether the read CSV has a header record.
    /// </summary>
    /// <seealso cref="HeaderBinder"/>
    public bool HasHeader
    {
        get => _hasHeader;
        set => SetValue(ref _hasHeader, value);
    }

    /// <summary>
    /// Custom header binder used in place of <see cref="HeaderTextBinder"/> or <see cref="HeaderUtf8Binder"/>
    /// if <see cref="HasHeader"/> is true.
    /// </summary>
    /// <remarks>
    /// By default, CSV header is matched to property/field names and
    /// <see cref="Binding.Attributes.CsvHeaderAttribute"/> using <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </remarks>
    public IHeaderBinder<T>? HeaderBinder
    {
        get => _headerBinder;
        set => SetValue(ref _headerBinder, value);
    }

    /// <summary>
    /// Pool used to create reusable buffers when needed. Default is <see cref="ArrayPool{T}.Shared"/>.
    /// Set to <see langword="null"/> to disable pooling and always allocate.
    /// </summary>
    public ArrayPool<T>? ArrayPool
    {
        get => _arrayPool;
        set => SetValue(ref _arrayPool, value);
    }

    /// <summary>
    /// Collection of all parsers and factories of the options instance.
    /// </summary>
    /// <remarks>
    /// Modifying the collection after the options instance is used (<see cref="IsReadOnly"/> is <see langword="true"/>)
    /// results in an exception.
    /// </remarks>
    /// <exception cref="InvalidOperationException"/>
    public IList<ICsvParser<T>> Parsers => _parsers ?? GetOrInitParsers();

    private ParserList? _parsers;
    private readonly ConcurrentDictionary<Type, ICsvParser<T>> _parserCache = new();

    /// <summary>
    /// Returns the default parsers that are used to initialize <see cref="Parsers"/> in derived types.
    /// </summary>
    protected virtual IEnumerable<ICsvParser<T>> GetDefaultParsers() => Enumerable.Empty<ICsvParser<T>>();

    [MethodImpl(MethodImplOptions.NoInlining)]
    [MemberNotNull(nameof(_parsers))]
    private ParserList GetOrInitParsers()
    {
        if (_parsers is not null)
            return _parsers;

        var parserList = new ParserList(this);
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

    /// <summary>
    /// Returns a parser for parsing values of the parameter type, or null if there is no
    /// parser registered for <paramref name="resultType"/>.
    /// </summary>
    /// <param name="resultType">Type to parse</param>
    public ICsvParser<T>? TryGetParser(Type resultType)
    {
        ArgumentNullException.ThrowIfNull(resultType);

        if (!IsReadOnly)
            MakeReadOnly();

        if (_parserCache.TryGetValue(resultType, out var cached))
        {
            return cached;
        }

        var parsers = GetOrInitParsers().Span;

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
        if (!IsReadOnly)
            MakeReadOnly();

        return GetOrInitParsers().Span;
    }

    /// <summary>
    /// Sets the value of <paramref name="field"/> after ensuring that the current instance is not read-only.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="field">Reference to the field to modify</param>
    /// <param name="value">Value to set</param>
    /// <param name="memberName">Name of the property being set, used in exception messages</param>
    /// <exception cref="InvalidOperationException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void SetValue<TValue>(ref TValue field, TValue value, [CallerMemberName] string memberName = "")
    {
        ThrowIfReadOnly(memberName);
        field = value;
    }

    /// <summary>
    /// Throws if <see cref="IsReadOnly"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="memberName">Name of the calling property or method used in exception messages</param>
    /// <exception cref="InvalidOperationException"></exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfReadOnly([CallerMemberName] string memberName = "")
    {
        if (IsReadOnly)
            ThrowForIsReadOnly(memberName);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForIsReadOnly(string memberName)
    {
        throw new InvalidOperationException($"The options-instance is read only (accessed via {memberName}).");
    }
}
