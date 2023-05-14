namespace FlameCsv.SourceGen;

internal readonly struct KnownSymbols
{
    public INamedTypeSymbol CsvConverterFactory => _icsvConverterFactory.Value;
    public INamedTypeSymbol CsvTypeMapAttribute => _typeMapAttribute.Value;
    public INamedTypeSymbol CsvHeaderIgnoreAttribute => _csvHeaderIgnoreAttribute.Value;
    public INamedTypeSymbol CsvHeaderAttribute => _csvHeaderAttribute.Value;
    public INamedTypeSymbol CsvConverterOfTAttribute => _csvConverterOfTAttribute.Value;
    public INamedTypeSymbol CsvConstructorAttribute => _csvConstructorAttribute.Value;

    private readonly LazySymbol _typeMapAttribute;
    private readonly LazySymbol _icsvConverterFactory;
    private readonly LazySymbol _csvHeaderIgnoreAttribute;
    private readonly LazySymbol _csvHeaderAttribute;
    private readonly LazySymbol _csvConverterOfTAttribute;
    private readonly LazySymbol _csvConstructorAttribute;

    public KnownSymbols(Compilation compilation)
    {
        _typeMapAttribute = new(compilation, "FlameCsv.Binding.CsvTypeMapAttribute`2", true);
        _icsvConverterFactory = new(compilation, "FlameCsv.CsvConverterFactory`1", true);
        _csvHeaderIgnoreAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderIgnoreAttribute");
        _csvHeaderAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderAttribute");
        _csvConverterOfTAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvConverterAttribute`2", true);
        _csvConstructorAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvConstructorAttribute");
    }
}
