using System.Collections.Concurrent;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Parsers;

namespace FlameCsv;

/// <summary>
/// Represents the configuration used to read and parse CSV.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public sealed partial class CsvReaderOptions<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Tokens used for CSV parsing. Defaults to <see cref="CsvTokens{T}.Windows"/> on supported types of
    /// <typeparamref name="T"/> and to uninitialized on unsupported types.
    /// </summary>
    /// <seealso cref="CsvTokens{T}.Windows"/>
    /// <exception cref="CsvConfigurationException">Thrown invalid options are set.</exception>
    public CsvTokens<T> Tokens
    {
        get => tokens;
        set => tokens = value.ThrowIfInvalid();
    }

    /// <summary>For internal use with "in"</summary>
    internal CsvTokens<T> tokens = CsvTokens<T>.GetDefaultForOptions();

    /// <summary>
    /// Delegate that determines whether a row should be skipped.
    /// Default is <see langword="null"/>, which means all rows are processed.
    /// </summary>
    public CsvCallback<T, bool>? ShouldSkipRow { get; set; }

    /// <summary>
    /// Flags determining if the CSV data can be exposed outside FlameCSV and the code running it.
    /// Default is <see cref="SecurityLevel.NoBufferClearing"/>, which allows pooled memory
    /// to be returned without clearing them.
    /// </summary>
    public SecurityLevel Security { get; set; } = SecurityLevel.NoBufferClearing;

    /// <summary>
    /// Whether the read CSV has a header record.
    /// </summary>
    /// <seealso cref="HeaderBinder"/>
    public bool HasHeader { get; set; }

    /// <summary>
    /// Custom header binder used in place of <see cref="HeaderTextBinder"/> or <see cref="HeaderUtf8Binder"/>
    /// if <see cref="HasHeader"/> is true.
    /// </summary>
    public IHeaderBinder<T>? HeaderBinder { get; set; }

    private readonly ConcurrentDictionary<Type, ICsvParser<T>> _parserCache = new();
    internal readonly List<ICsvParser<T>> _parsers = new();

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

        if (_parserCache.TryGetValue(resultType, out var cached))
        {
            return cached;
        }

        lock (_parsers)
        {
            Span<ICsvParser<T>> parsers = _parsers.AsSpan();

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
        }

        return null;
    }

    /// <summary>
    /// Removes all parsers added to the builder.
    /// </summary>
    /// <returns>The same options instance</returns>
    public CsvReaderOptions<T> ClearParsers()
    {
        lock (_parsers)
        {
            _parsers.Clear();
        }

        return this;
    }

    /// <summary>
    /// Removes all for which <see cref="ICsvParser{T}.CanParse"/> returns <see langword="true"/>
    /// for <typeparamref name="TValue"/>.
    /// </summary>
    /// <returns>The same options instance</returns>
    public CsvReaderOptions<T> RemoveParsers<TValue>()
    {
        lock (_parsers)
        {
            _parsers.RemoveAll(static p => p.CanParse(typeof(TValue)));
        }

        return this;
    }

    /// <summary>
    /// Adds the parser to the builder.
    /// </summary>
    /// <remarks>
    /// Parsers are prioritized in "last in, first out"-order, so the last parser added will be the first one
    /// checked. This also means that built-in parsers can be "overridden" by simply adding a new parser
    /// for the specific type afterwards.
    /// </remarks>
    /// <param name="parser">Parser to add</param>
    /// <returns>The same options instance</returns>
    public CsvReaderOptions<T> AddParser(ICsvParser<T> parser)
    {
        ArgumentNullException.ThrowIfNull(parser);

        lock (_parsers)
        {
            _parsers.Add(parser);
        }

        return this;
    }

    /// <inheritdoc cref="AddParsers(IEnumerable{ICsvParser{T}})"/>
    public CsvReaderOptions<T> AddParsers(params ICsvParser<T>[] parsers)
    {
        return AddParsers(parsers as IEnumerable<ICsvParser<T>>);
    }

    /// <summary>
    /// Adds the parsers to the builder.
    /// </summary>
    /// <param name="parsers">Parsers to add</param>
    /// <returns>The same options instance</returns>
    public CsvReaderOptions<T> AddParsers(IEnumerable<ICsvParser<T>> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);

        lock (_parsers)
        {
            foreach (var parser in parsers)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (parser is null)
                    ThrowHelper.ThrowArgumentException(nameof(parsers), "The enumerable contained null");
                _parsers.Add(parser);
            }
        }

        return this;
    }

    /// <summary>
    /// Sets the parameter to <see cref="ShouldSkipRow"/>.
    /// </summary>
    /// <returns>The same options instance</returns>
    public CsvReaderOptions<T> SetRowSkipPredicate(CsvCallback<T, bool>? predicate)
    {
        ShouldSkipRow = predicate;
        return this;
    }

    /// <summary>
    /// Sets the parameter to <see cref="Tokens"/>.
    /// </summary>
    /// <returns>The same options instance</returns>
    // ReSharper disable once ParameterHidesMember
    public CsvReaderOptions<T> SetTokens(in CsvTokens<T> tokens)
    {
        this.tokens = tokens;
        return this;
    }
}
