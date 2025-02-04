namespace FlameCsv.SourceGen;

// ReSharper disable InconsistentNaming
// ref struct to avoid accidental storage
internal readonly ref struct FlameSymbols
{
    private INamedTypeSymbol CsvOptions { get; }
    private INamedTypeSymbol CsvConverterTTValue { get; }
    private INamedTypeSymbol CsvConverterFactory { get; }
    private INamedTypeSymbol CsvConverterOfTAttribute { get; }
    private INamedTypeSymbol CsvFieldAttribute { get; }
    private INamedTypeSymbol CsvTypeFieldAttribute { get; }
    private INamedTypeSymbol CsvTypeAttribute { get; }
    private INamedTypeSymbol CsvConstructorAttribute { get; }
    private INamedTypeSymbol CsvAssemblyTypeAttribute { get; }
    private INamedTypeSymbol CsvAssemblyTypeFieldAttribute { get; }

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
            _factoryTypes[tokenType]
                = type = CsvConverterFactory.ConstructedFrom.Construct(tokenType.OriginalDefinition);
        }

        return type;
    }

    // @formatter:off
    public bool IsCsvOptions([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvOptions, symbol);
    public bool IsCsvConverterTTValue([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvConverterTTValue, symbol);
    public bool IsCsvConverterFactory([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvConverterFactory, symbol);
    public bool IsCsvConverterOfTAttribute([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvConverterOfTAttribute, symbol);
    public bool IsCsvFieldAttribute([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvFieldAttribute, symbol);
    public bool IsCsvTypeFieldAttribute([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvTypeFieldAttribute, symbol);
    public bool IsCsvTypeAttribute([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvTypeAttribute, symbol);
    public bool IsCsvConstructorAttribute([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvConstructorAttribute, symbol);
    public bool IsCsvAssemblyTypeAttribute([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvAssemblyTypeAttribute, symbol);
    public bool IsCsvAssemblyTypeFieldAttribute([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvAssemblyTypeFieldAttribute, symbol);
    // @formatter:on

    private static INamedTypeSymbol Get(Compilation compilation, string name)
    {
        return compilation.GetTypeByMetadataName(name) ??
            throw new InvalidOperationException("Type not found by metadata name: " + name);
    }

    private static INamedTypeSymbol GetUnboundGeneric(Compilation compilation, string name)
        => Get(compilation, name).ConstructUnboundGenericType();
}
