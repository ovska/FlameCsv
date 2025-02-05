namespace FlameCsv.SourceGen;

// ReSharper disable InconsistentNaming

// ref struct to avoid accidental storage
internal readonly ref struct FlameSymbols
{
    public ITypeSymbol TargetType { get; }

#if USE_COMPILATION
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
#endif

    public FlameSymbols(
#if USE_COMPILATION
        Compilation compilation,
#endif
        ITypeSymbol targetType)
    {
        TargetType = targetType;

#if USE_COMPILATION
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
#endif
    }

#if USE_COMPILATION
    public bool IsCsvOptionsOfT(ITypeSymbol tokenType, ITypeSymbol symbol)
    {
        if (!_optionsTypes.TryGetValue(tokenType, out INamedTypeSymbol? type))
        {
            _optionsTypes[tokenType] = type = CsvOptions.OriginalDefinition.Construct(tokenType.OriginalDefinition);
        }

        return SymbolEqualityComparer.Default.Equals(symbol, type);
    }

    public bool IsGetCsvConverterFactoryOfT(ITypeSymbol tokenType, ITypeSymbol symbol)
    {
        if (!_factoryTypes.TryGetValue(tokenType, out INamedTypeSymbol? type))
        {
            _factoryTypes[tokenType]
                = type = CsvConverterFactory.ConstructedFrom.Construct(tokenType.OriginalDefinition);
        }

        return SymbolEqualityComparer.Default.Equals(symbol, type);
    }
#else
    // ReSharper disable MemberCanBeMadeStatic.Global
    public bool IsCsvOptionsOfT(ITypeSymbol tokenType, ITypeSymbol symbol)
    {
        return IsGenericTypeOfT(tokenType, symbol, "FlameCsv.CsvOptions<");
    }

    public bool IsGetCsvConverterFactoryOfT(ITypeSymbol tokenType, ITypeSymbol symbol)
    {
        return IsGenericTypeOfT(tokenType, symbol, "FlameCsv.CsvConverterFactory<");
    }

    private static bool IsGenericTypeOfT(ITypeSymbol tokenType, ITypeSymbol symbol, string prefix)
    {
        if (symbol.SpecialType is SpecialType.System_Object || symbol is INamedTypeSymbol { Arity: not 1 })
        {
            return false;
        }

        string tokenString = tokenType.ToDisplayString();
        string symbolString = symbol.ToDisplayString();

        return
            symbolString.Length == tokenString.Length + prefix.Length + 1 &&
            symbolString.StartsWith(prefix) &&
            symbolString.IndexOf(tokenString, StringComparison.Ordinal) == prefix.Length &&
            symbolString[prefix.Length + tokenString.Length] == '>';
    }
    // ReSharper restore MemberCanBeMadeStatic.Global
#endif

#if USE_COMPILATION
    // @formatter:off
    public bool IsCsvConverterTTValue([NotNullWhen(true)] ISymbol? symbol) => SymbolEqualityComparer.Default.Equals(CsvConverterTTValue, symbol);
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
#else
    // ReSharper disable MemberCanBeMadeStatic.Global
    // @formatter:off
    public bool IsCsvConverterTTValue([NotNullWhen(true)] ISymbol? symbol) => _csvConverterTTValue.Equals(symbol);
    public bool IsCsvConverterOfTAttribute([NotNullWhen(true)] ISymbol? symbol) => _csvConverterOfTAttribute.Equals(symbol);
    public bool IsCsvFieldAttribute([NotNullWhen(true)] ISymbol? symbol) => _csvFieldAttribute.Equals(symbol);
    public bool IsCsvTypeFieldAttribute([NotNullWhen(true)] ISymbol? symbol) => _csvTypeFieldAttribute.Equals(symbol);
    public bool IsCsvTypeAttribute([NotNullWhen(true)] ISymbol? symbol) => _csvTypeAttribute.Equals(symbol);
    public bool IsCsvConstructorAttribute([NotNullWhen(true)] ISymbol? symbol) => _csvConstructorAttribute.Equals(symbol);
    public bool IsCsvAssemblyTypeAttribute([NotNullWhen(true)] ISymbol? symbol) => _csvAssemblyTypeAttribute.Equals(symbol);
    public bool IsCsvAssemblyTypeFieldAttribute([NotNullWhen(true)] ISymbol? symbol) => _csvAssemblyTypeFieldAttribute.Equals(symbol);

    private static readonly TypeMetadata _csvConverterTTValue = new("FlameCsv.CsvConverter", 2);
    private static readonly TypeMetadata _csvConverterOfTAttribute = new("FlameCsv.Binding.Attributes.CsvConverterAttribute", 2);
    private static readonly TypeMetadata _csvFieldAttribute = new("FlameCsv.Binding.Attributes.CsvFieldAttribute");
    private static readonly TypeMetadata _csvTypeFieldAttribute = new("FlameCsv.Binding.Attributes.CsvTypeFieldAttribute");
    private static readonly TypeMetadata _csvTypeAttribute = new("FlameCsv.Binding.Attributes.CsvTypeAttribute");
    private static readonly TypeMetadata _csvConstructorAttribute = new("FlameCsv.Binding.Attributes.CsvConstructorAttribute");
    private static readonly TypeMetadata _csvAssemblyTypeAttribute = new("FlameCsv.Binding.Attributes.CsvAssemblyTypeAttribute");
    private static readonly TypeMetadata _csvAssemblyTypeFieldAttribute = new("FlameCsv.Binding.Attributes.CsvAssemblyTypeFieldAttribute");
    // @formatter:on
    // ReSharper restore MemberCanBeMadeStatic.Global
#endif
}


#if !USE_COMPILATION
internal readonly record struct TypeMetadata
{
    public int Arity { get; }
    public string FullName { get; }
    public string Name { get; }
    public string Namespace { get; }

    public TypeMetadata(string fullName, int genericParameterCount = 0)
    {
        int namespaceIndex = fullName.LastIndexOf('.');

        Arity = genericParameterCount;
        FullName = fullName;
        Name = fullName.Substring(namespaceIndex + 1);
        Namespace = namespaceIndex == -1 ? "" : fullName.Substring(0, namespaceIndex);
    }

    public bool Equals([NotNullWhen(true)] ISymbol? symbol)
    {
        if (symbol is not null)
        {
            if (symbol.Name == Name && (Arity == 0 || symbol is INamedTypeSymbol namedType && namedType.Arity == Arity))
            {
                return symbol.ContainingNamespace.ToDisplayString() == Namespace;
            }
        }

        return false;
    }
}
#endif
