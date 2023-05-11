namespace FlameCsv.SourceGen;

internal readonly struct KnownSymbols
{
    public INamedTypeSymbol ICsvParserFactory => _icsvParserFactory.Value;
    public INamedTypeSymbol CsvTypeMapAttribute => _typeMapAttribute.Value;
    public INamedTypeSymbol CsvHeaderIgnoreAttribute => _csvHeaderIgnoreAttribute.Value;
    public INamedTypeSymbol CsvHeaderAttribute => _csvHeaderAttribute.Value;
    public INamedTypeSymbol CsvParserOverrideOfTAttribute => _csvParserOverrideOfTAttribute.Value;
    public INamedTypeSymbol CsvConstructorAttribute => _csvConstructorAttribute.Value;

    private readonly LazySymbol _typeMapAttribute;
    private readonly LazySymbol _icsvParserFactory;
    private readonly LazySymbol _csvHeaderIgnoreAttribute;
    private readonly LazySymbol _csvHeaderAttribute;
    private readonly LazySymbol _csvParserOverrideOfTAttribute;
    private readonly LazySymbol _csvConstructorAttribute;

    public KnownSymbols(Compilation compilation)
    {
        _typeMapAttribute = new(compilation, "FlameCsv.Binding.CsvTypeMapAttribute`2", true);
        _icsvParserFactory = new(compilation, "FlameCsv.Parsers.ICsvParserFactory`1", true);
        _csvHeaderIgnoreAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderIgnoreAttribute");
        _csvHeaderAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderAttribute");
        _csvParserOverrideOfTAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvParserOverrideAttribute`2", true);
        _csvConstructorAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvConstructorAttribute");
    }
}
