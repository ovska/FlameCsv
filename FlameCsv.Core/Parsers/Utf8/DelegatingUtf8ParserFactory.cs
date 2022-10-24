using FlameCsv.Runtime;

namespace FlameCsv.Parsers.Utf8;

public sealed class DelegatingUtf8ParserFactory : ICsvParserFactory<byte>
{
    private readonly CsvConfiguration<char> _inner;

    public DelegatingUtf8ParserFactory(CsvConfiguration<char> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public bool CanParse(Type resultType)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery - avoid closure allocation
        foreach (var parser in _inner._parsers)
        {
            if (parser.CanParse(resultType))
                return true;
        }

        return false;
    }

    public ICsvParser<byte> Create(Type resultType, CsvConfiguration<byte> configuration)
    {
        return ActivatorEx.CreateInstance<ICsvParser<byte>>(
            typeof(DelegatingUtf8Parser<>).MakeGenericType(resultType),
            configuration.GetParser(resultType));
    }
}
