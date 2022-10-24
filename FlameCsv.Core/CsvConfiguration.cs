using System.Collections.Concurrent;
using System.Collections.Immutable;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Parsers;

namespace FlameCsv;

/// <summary>
/// Represents a built CSV read-only CSV configuration.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public sealed partial class CsvConfiguration<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// CSV parsing options, such as delimiters and line breaks. Defaults to environment specific.
    /// </summary>
    public CsvParserOptions<T> Options => _options;

    internal readonly CsvParserOptions<T> _options;

    /// <summary>
    /// Delegate that determines whether a row should be skipped.
    /// Default is <see cref="DefaultRowSkipPredicate"/>. If null, all rows are processed.
    /// </summary>
    public CsvCallback<T, bool>? ShouldSkipRow { get; }

    /// <summary>
    /// Flags determining if the CSV data can be exposed outside FlameCSV and the code running it.
    /// Default is <see cref="SecurityLevel.NoBufferClearing"/>, which allows pooled memory
    /// to be returned without clearing them.
    /// </summary>
    public SecurityLevel Security { get; }

    private readonly ConcurrentDictionary<Type, ICsvParser<T>> _cache = new();
    internal readonly ImmutableArray<ICsvParser<T>> _parsers;

    /// <inheritdoc cref="CsvConfigurationBuilder{T}.Build"/>
    public CsvConfiguration(CsvConfigurationBuilder<T> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder._parsers.Count == 0)
            throw new CsvConfigurationException("No parsers in the configuration builder.");

        _options = builder.Options.ThrowIfInvalid();

        ShouldSkipRow = builder.ShouldSkipRow;
        Security = builder.Security;

        // HACK: reverse priority; parsers added last (user added) have the most weight
        var parsers = builder._parsers.ToArray();
        Array.Reverse(parsers);
        _parsers = ImmutableArray.Create(parsers);
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

        if (_cache.TryGetValue(resultType, out var cached))
        {
            return cached;
        }

        foreach (var instance in _parsers)
        {
            if (instance.CanParse(resultType))
            {
                var parser = instance.GetParserOrFromFactory(resultType, this);
                _cache.TryAdd(resultType, parser);
                return parser;
            }
        }

        return null;
    }
}
