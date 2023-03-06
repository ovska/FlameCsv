using FlameCsv.Runtime;

namespace FlameCsv.Parsers;

/// <summary>
/// Factory for <see cref="NullableParser{T,TValue}"/>
/// </summary>
public sealed class NullableParserFactory<T> : ICsvParserFactory<T>
    where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Token to match null to if the parsing fails.
    /// </summary>
    /// <remarks>
    /// If the <see cref="CsvReaderOptions{T}"/>-instance used when implements <see cref="ICsvNullTokenProvider{T}"/>
    /// the provider's configuration is used instead.
    /// </remarks>
    public ReadOnlyMemory<T> NullToken { get; }

    public NullableParserFactory(ReadOnlyMemory<T> nullToken = default)
    {
        NullToken = nullToken;
    }

    public bool CanParse(Type resultType)
    {
        return resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    public ICsvParser<T> Create(Type resultType, CsvReaderOptions<T> options)
    {
        var nullToken = NullToken;

        if (options is ICsvNullTokenProvider<T> nullableProvider)
        {
            nullToken = nullableProvider.TryGetOverride(resultType, out var @override)
                ? @override
                : nullableProvider.Default;
        }

        var innerType = Nullable.GetUnderlyingType(resultType)!;
        var inner = options.GetParser(innerType);
        return ActivatorEx.CreateInstance<ICsvParser<T>>(
            typeof(NullableParser<,>).MakeGenericType(typeof(T), innerType),
            inner,
            nullToken);
    }

    public static NullableParserFactory<T> Instance { get; } = new();

    internal static NullableParserFactory<T> GetOrCreate(ReadOnlyMemory<T> nullToken)
    {
        return nullToken.IsEmpty ? Instance : (new(nullToken));
    }
}
