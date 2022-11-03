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
    public ReadOnlyMemory<T> NullToken { get; }

    public NullableParserFactory(ReadOnlyMemory<T> nullToken = default)
    {
        NullToken = nullToken;
    }

    public bool CanParse(Type resultType) => Nullable.GetUnderlyingType(resultType) is not null;

    public ICsvParser<T> Create(Type resultType, CsvReaderOptions<T> options)
    {
        var innerType = Nullable.GetUnderlyingType(resultType)!;
        var inner = options.GetParser(innerType);
        return ActivatorEx.CreateInstance<ICsvParser<T>>(
            typeof(NullableParser<,>).MakeGenericType(typeof(T), innerType),
            inner,
            NullToken);
    }
}
