using CommunityToolkit.Diagnostics;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Providers;
using FlameCsv.Parsers;

namespace FlameCsv;

public sealed class CsvConfigurationBuilder<T> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Contains the parsers and factories. Parsers are prioritized in "last in, first out", i.e. the last parser
    /// in the list is the first to be checked for any given type.
    /// </summary>
    /// <remarks>
    /// To override a parser for a specific member, use <see cref="CsvParserOverrideAttribute"/>.
    /// </remarks>
    public IList<ICsvParser<T>> Parsers => _parsers;

    /// <summary>
    /// The provider that binds CSV columns to members of the parsed type.<para/>
    /// Defaults to binding header columns to member names if <typeparamref name="T"/> is <see langword="char"/>
    /// or <see langword="byte"/>.
    /// </summary>
    /// <remarks>
    /// To use multiple bindings and use the first one that succeeds, see <see cref="MultiBindingProvider{T}"/>
    /// and <see cref="MultiHeaderBindingProvider{T}"/>.
    /// </remarks>
    public ICsvBindingProvider<T> BindingProvider { get; set; } = GetDefaultBinder();

    private CsvParserOptions<T>? _options;
    internal readonly List<ICsvParser<T>> _parsers = new();

    /// <inheritdoc cref="CsvConfiguration{T}.Options"/>
    public CsvParserOptions<T> Options
    {
        // lazy initialization to avoid throwing for exotic token types when the class is initialized
        get => _options ??= CsvParserOptions<T>.Windows;
        set => _options = value;
    }

    /// <inheritdoc cref="CsvConfiguration{T}.ShouldSkipRow"/>
    public CsvCallback<T, bool>? ShouldSkipRow { get; set; } = CsvConfiguration<T>.DefaultRowSkipPredicate;

    /// <inheritdoc cref="CsvConfiguration{T}.Security"/>
    public SecurityLevel Security { get; set; } = SecurityLevel.NoBufferClearing;

    /// <summary>
    /// Removes all parsers added to the builder.
    /// </summary>
    /// <returns>The same builder instance</returns>
    public CsvConfigurationBuilder<T> ClearParsers()
    {
        _parsers.Clear();
        return this;
    }

    /// <summary>
    /// Adds the parser to the builder.
    /// </summary>
    /// <param name="parser">Parser to add</param>
    /// <returns>The same builder instance</returns>
    public CsvConfigurationBuilder<T> AddParser(ICsvParser<T> parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        _parsers.Add(parser);
        return this;
    }

    /// <inheritdoc cref="AddParsers(IEnumerable{ICsvParser{T}})"/>
    public CsvConfigurationBuilder<T> AddParsers(params ICsvParser<T>[] parsers)
    {
        return AddParsers(parsers.AsEnumerable());
    }

    /// <summary>
    /// Adds the parsers to the builder.
    /// </summary>
    /// <param name="parsers">Parsers to add</param>
    /// <returns>The same builder instance</returns>
    public CsvConfigurationBuilder<T> AddParsers(IEnumerable<ICsvParser<T>> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);

        foreach (var parser in parsers)
        {
            Guard.IsNotNull(parser);
            _parsers.Add(parser);
        }

        return this;
    }

    /// <summary>
    /// Sets the parameter to <see cref="BindingProvider"/>.
    /// </summary>
    /// <returns>The same builder instance</returns>
    public CsvConfigurationBuilder<T> SetBinder(ICsvBindingProvider<T> bindingProvider)
    {
        BindingProvider = bindingProvider;
        return this;
    }

    /// <summary>
    /// Sets the parameter to <see cref="ShouldSkipRow"/>.
    /// </summary>
    /// <returns>The same builder instance</returns>
    public CsvConfigurationBuilder<T> SetRowSkipPredicate(CsvCallback<T, bool>? predicate)
    {
        ShouldSkipRow = predicate;
        return this;
    }

    /// <summary>
    /// Sets the parameter to <see cref="Options"/>.
    /// </summary>
    /// <returns>The same builder instance</returns>
    public CsvConfigurationBuilder<T> SetParserOptions(CsvParserOptions<T> options)
    {
        Options = options;
        return this;
    }

    /// <summary>
    /// Builds the configuration and validates the parameters.
    /// </summary>
    /// <returns>Built read-only configuration instance</returns>
    public CsvConfiguration<T> Build() => new(this);

    private static ICsvBindingProvider<T> GetDefaultBinder()
    {
        object obj;

        if (typeof(T) == typeof(char))
            obj = new HeaderTextBindingProvider<char>();
        else if (typeof(T) == typeof(byte))
            obj = new HeaderUtf8BindingProvider<char>();
        else
            obj = NotSupportedBindingProvider<char>.Instance;

        return (ICsvBindingProvider<T>)obj;
    }
}
