namespace FlameCsv.SourceGen;

internal readonly struct KnownSymbols(Compilation compilation)
{
    public bool Nullable => compilation.Options.NullableContextOptions.AnnotationsEnabled();

    public INamedTypeSymbol CsvOptions { get; } = GetUnboundGeneric(compilation, "FlameCsv.CsvOptions`1");
    public INamedTypeSymbol CsvConverterFactory { get; } = GetUnboundGeneric(compilation, "FlameCsv.CsvConverterFactory`1");
    public INamedTypeSymbol CsvConverterOfTAttribute { get; } = GetUnboundGeneric(compilation, "FlameCsv.Binding.Attributes.CsvConverterAttribute`2");

    public INamedTypeSymbol CsvHeaderIgnoreAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvHeaderIgnoreAttribute");
    public INamedTypeSymbol CsvHeaderAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvHeaderAttribute");
    public INamedTypeSymbol CsvHeaderTargetAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvHeaderTargetAttribute");
    public INamedTypeSymbol CsvConstructorAttribute => Get(compilation, "FlameCsv.Binding.Attributes.CsvConstructorAttribute");
    public INamedTypeSymbol SystemDateTime => Get(compilation, "System.DateTime");
    public INamedTypeSymbol SystemDateTimeOffset => Get(compilation, "System.DateTimeOffset");
    public INamedTypeSymbol SystemTimeSpan => Get(compilation, "System.TimeSpan");
    public INamedTypeSymbol SystemGuid => Get(compilation, "System.Guid");

    private readonly Dictionary<ISymbol, INamedTypeSymbol> _optionsTypes = new(SymbolEqualityComparer.Default);

    public INamedTypeSymbol GetCsvOptionsType(ITypeSymbol tokenType)
    {
        if (!_optionsTypes.TryGetValue(tokenType, out INamedTypeSymbol? type))
        {
            _optionsTypes[tokenType] = type = CsvOptions.OriginalDefinition.Construct(tokenType.OriginalDefinition);
        }

        return type;
    }

    /// <summary>
    /// Returns CsvUtf8Options or CsvTextOptions, or null
    /// </summary>
    public INamedTypeSymbol? GetExplicitOptionsType(ITypeSymbol tokenType)
    {
        return tokenType.SpecialType switch
        {
            SpecialType.System_Byte => Get(compilation, "FlameCsv.CsvUtf8Options"),
            SpecialType.System_Char => Get(compilation, "FlameCsv.CsvTextOptions"),
            _ => null
        };
    }

    private static INamedTypeSymbol Get(Compilation compilation, string name)
    {
        return compilation.GetTypeByMetadataName(name)
            ?? throw new InvalidOperationException("Type not found by metadata name: " + name);
    }

    private static INamedTypeSymbol GetUnboundGeneric(Compilation compilation, string name) => Get(compilation, name).ConstructUnboundGenericType();
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
