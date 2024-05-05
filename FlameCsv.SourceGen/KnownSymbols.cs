namespace FlameCsv.SourceGen;

internal readonly struct KnownSymbols(Compilation compilation)
{
    public bool Nullable => compilation.Options.NullableContextOptions.AnnotationsEnabled();

    public INamedTypeSymbol CsvConverterFactory => _csvConverterFactory.Value;
    public INamedTypeSymbol CsvOptions => _csvOptions.Value;
    public INamedTypeSymbol CsvTypeMapAttribute => _typeMapAttribute.Value;
    public INamedTypeSymbol CsvHeaderIgnoreAttribute => _csvHeaderIgnoreAttribute.Value;
    public INamedTypeSymbol CsvHeaderAttribute => _csvHeaderAttribute.Value;
    public INamedTypeSymbol CsvHeaderTargetAttribute => _csvHeaderTargetAttribute.Value;
    public INamedTypeSymbol CsvConverterOfTAttribute => _csvConverterOfTAttribute.Value;
    public INamedTypeSymbol CsvConstructorAttribute => _csvConstructorAttribute.Value;

    private readonly LazySymbol _typeMapAttribute = new(compilation, "FlameCsv.Binding.CsvTypeMapAttribute`2", true);
    private readonly LazySymbol _csvOptions = new(compilation, "FlameCsv.CsvOptions`1", true);
    private readonly LazySymbol _csvConverterFactory = new(compilation, "FlameCsv.CsvConverterFactory`1", true);
    private readonly LazySymbol _csvHeaderIgnoreAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderIgnoreAttribute");
    private readonly LazySymbol _csvHeaderAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderAttribute");
    private readonly LazySymbol _csvHeaderTargetAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvHeaderTargetAttribute");
    private readonly LazySymbol _csvConverterOfTAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvConverterAttribute`2", true);
    private readonly LazySymbol _csvConstructorAttribute = new(compilation, "FlameCsv.Binding.Attributes.CsvConstructorAttribute");

    private readonly Dictionary<ISymbol, INamedTypeSymbol> _optionsTypes = new(SymbolEqualityComparer.Default);

    public INamedTypeSymbol GetCsvOptionsType(ITypeSymbol tokenType)
    {
        if (!_optionsTypes.TryGetValue(tokenType, out INamedTypeSymbol? type))
        {
            _optionsTypes[tokenType] = type = CsvOptions.OriginalDefinition.Construct(tokenType.OriginalDefinition);
        }

        return type;
    }
}

internal readonly struct SymbolMetadata
{
    public ISymbol Symbol { get; }
    public IEnumerable<string> Names { get; }
    public bool IsRequired { get; }
    public int Order { get; }
    public BindingScope Scope { get; }

    public SymbolMetadata(ISymbol symbol, in KnownSymbols knownSymbols)
    {
        Symbol = symbol;

        foreach (var attributeData in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, knownSymbols.CsvHeaderAttribute))
            {
                // params-array
                if (attributeData.ConstructorArguments[0].Values is { IsDefaultOrEmpty: false } namesArray)
                {
                    var names = new string[namesArray.Length];

                    for (int i = 0; i < namesArray.Length; i++)
                        names[i] = namesArray[i].Value as string ?? "";

                    Names = names;
                }

                foreach (var argument in attributeData.NamedArguments)
                {
                    switch (argument)
                    {
                        case { Key: "Required", Value.Value: bool _required }:
                            IsRequired = _required;
                            break;
                        case { Key: "Order", Value.Value: int _order }:
                            Order = _order;
                            break;
                        case { Key: "Scope", Value.Value: int _scope }:
                            Scope = (BindingScope)_scope;
                            break;
                    }
                }

                break;
            }
        }

        Names ??= new[] { symbol.Name };
    }
}
