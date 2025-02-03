namespace FlameCsv.SourceGen;

// ReSharper disable InconsistentNaming
// ref struct to avoid accidental storage
internal readonly ref struct FlameSymbols
{
    public INamedTypeSymbol CsvOptions { get; }
    public INamedTypeSymbol CsvConverterTTValue { get; }
    public INamedTypeSymbol CsvConverterFactory { get; }
    public INamedTypeSymbol CsvConverterOfTAttribute { get; }
    public INamedTypeSymbol CsvFieldAttribute { get; }
    public INamedTypeSymbol CsvTypeFieldAttribute { get; }
    public INamedTypeSymbol CsvTypeAttribute { get; }
    public INamedTypeSymbol CsvConstructorAttribute { get; }
    public INamedTypeSymbol CsvAssemblyTypeAttribute { get; }
    public INamedTypeSymbol CsvAssemblyTypeFieldAttribute { get; }

    private readonly Dictionary<ISymbol, INamedTypeSymbol> _optionsTypes = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ISymbol, INamedTypeSymbol> _factoryTypes = new(SymbolEqualityComparer.Default);

    public FlameSymbols(Compilation compilation)
    {
        // @formatter:off
        CsvOptions = GetUnboundGeneric(compilation, "FlameCsv.CsvOptions`1");
        CsvConverterTTValue = GetUnboundGeneric(compilation, "FlameCsv.CsvConverter`2");
        CsvConverterFactory = GetUnboundGeneric(compilation, "FlameCsv.CsvConverterFactory`1");
        CsvConverterOfTAttribute = GetUnboundGeneric(compilation, "FlameCsv.Binding.Attributes.CsvConverterAttribute`2");
        CsvFieldAttribute = Get(compilation, "FlameCsv.Binding.Attributes.CsvFieldAttribute");
        CsvTypeFieldAttribute = Get(compilation, "FlameCsv.Binding.Attributes.CsvTypeFieldAttribute");
        CsvTypeAttribute = Get(compilation, "FlameCsv.Binding.Attributes.CsvTypeAttribute");
        CsvConstructorAttribute = Get(compilation, "FlameCsv.Binding.Attributes.CsvConstructorAttribute");
        CsvAssemblyTypeAttribute = Get(compilation, "FlameCsv.Binding.Attributes.CsvAssemblyTypeAttribute");
        CsvAssemblyTypeFieldAttribute = Get(compilation, "FlameCsv.Binding.Attributes.CsvAssemblyTypeFieldAttribute");
        // @formatter:on
    }

    public INamedTypeSymbol GetCsvOptionsType(ITypeSymbol tokenType)
    {
        if (!_optionsTypes.TryGetValue(tokenType, out INamedTypeSymbol? type))
        {
            _optionsTypes[tokenType] = type = CsvOptions.OriginalDefinition.Construct(tokenType.OriginalDefinition);
        }

        return type;
    }

    public INamedTypeSymbol GetCsvConverterFactoryType(ITypeSymbol tokenType)
    {
        if (!_factoryTypes.TryGetValue(tokenType, out INamedTypeSymbol? type))
        {
            _factoryTypes[tokenType] = type = CsvConverterFactory.ConstructedFrom.Construct(tokenType.OriginalDefinition);
        }

        return type;
    }

    private static INamedTypeSymbol Get(Compilation compilation, string name)
    {
        return compilation.GetTypeByMetadataName(name) ??
            throw new InvalidOperationException("Type not found by metadata name: " + name);
    }

    private static INamedTypeSymbol GetUnboundGeneric(Compilation compilation, string name)
        => Get(compilation, name).ConstructUnboundGenericType();
}
