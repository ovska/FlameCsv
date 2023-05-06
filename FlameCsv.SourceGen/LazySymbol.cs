namespace FlameCsv.SourceGen;

internal sealed class LazySymbol : Lazy<INamedTypeSymbol>
{
    public LazySymbol(Compilation compilation, string name, bool unboundGeneric = false)
        : base(
            () =>
            {
                try
                {
                    var symbol = compilation.GetTypeByMetadataName(name)
                        ?? throw new Exception($"Type '{name}' not found from metadata.");

                    if (unboundGeneric)
                    {
                        symbol = symbol.ConstructUnboundGenericType();
                    }

                    return symbol;
                }
                catch (Exception e)
                {
                    throw new Exception("Error, could not find " + name, e);
                }
            },
            LazyThreadSafetyMode.ExecutionAndPublication)
    {
    }
}

internal readonly struct KnownSymbols
{
    public INamedTypeSymbol ICsvParserFactory => _icsvParserFactory.Value;
    public INamedTypeSymbol CsvTypeMapAttribute => _typeMapAttribute.Value;
    public INamedTypeSymbol CsvHeaderIgnoreAttribute => _csvHeaderIgnoreAttribute.Value;
    public INamedTypeSymbol CsvHeaderAttribute => _csvHeaderAttribute.Value;
    public INamedTypeSymbol CsvHeaderRequiredAttribute => _csvHeaderRequiredAttribute.Value;
    public INamedTypeSymbol CsvParserOverrideOfTAttribute => _csvParserOverrideOfTAttribute.Value;

    private readonly LazySymbol _typeMapAttribute;
    private readonly LazySymbol _icsvParserFactory;
    private readonly LazySymbol _csvHeaderIgnoreAttribute;
    private readonly LazySymbol _csvHeaderAttribute;
    private readonly LazySymbol _csvHeaderRequiredAttribute;
    private readonly LazySymbol _csvParserOverrideOfTAttribute;

    public KnownSymbols(Compilation compilation)
    {
        _typeMapAttribute = new(compilation, "FlameCsv.Binding.CsvTypeMapAttribute`2", true);
        _icsvParserFactory = new(compilation, "FlameCsv.Parsers.ICsvParserFactory`1", true);
        _csvHeaderIgnoreAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderIgnoreAttribute");
        _csvHeaderAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderAttribute");
        _csvHeaderRequiredAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderRequiredAttribute");
        _csvParserOverrideOfTAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvParserOverrideAttribute`2", true);
    }
}
