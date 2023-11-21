namespace FlameCsv.SourceGen;

internal readonly struct KnownSymbols(Compilation compilation)
{
    public INamedTypeSymbol CsvConverterFactory => _icsvConverterFactory.Value;
    public INamedTypeSymbol CsvTypeMapAttribute => _typeMapAttribute.Value;
    public INamedTypeSymbol CsvHeaderIgnoreAttribute => _csvHeaderIgnoreAttribute.Value;
    public INamedTypeSymbol CsvHeaderAttribute => _csvHeaderAttribute.Value;
    public INamedTypeSymbol CsvConverterOfTAttribute => _csvConverterOfTAttribute.Value;
    public INamedTypeSymbol CsvConstructorAttribute => _csvConstructorAttribute.Value;

    private readonly LazySymbol _typeMapAttribute = new(compilation, "FlameCsv.Binding.CsvTypeMapAttribute`2", true);
    private readonly LazySymbol _icsvConverterFactory = new(compilation, "FlameCsv.CsvConverterFactory`1", true);
    private readonly LazySymbol _csvHeaderIgnoreAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderIgnoreAttribute");
    private readonly LazySymbol _csvHeaderAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderAttribute");
    private readonly LazySymbol _csvConverterOfTAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvConverterAttribute`2", true);
    private readonly LazySymbol _csvConstructorAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvConstructorAttribute");
}
