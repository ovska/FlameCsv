using FlameCsv.Runtime;

namespace FlameCsv.Parsers.Utf8;

public sealed class DelegatingUtf8ParserFactory : ICsvParserFactory<byte>
{
    private readonly CsvReaderOptions<char> _inner;

    public DelegatingUtf8ParserFactory(CsvReaderOptions<char> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    public bool CanParse(Type resultType)
    {
        foreach (var parser in _inner.EnumerateParsers())
        {
            if (parser.CanParse(resultType))
                return true;
        }

        return false;
    }

    public ICsvParser<byte> Create(Type resultType, CsvReaderOptions<byte> options)
    {
        return ActivatorEx.CreateInstance<ICsvParser<byte>>(
            typeof(DelegatingUtf8Parser<>).MakeGenericType(resultType),
            options.GetParser(resultType));
    }
}
