namespace FlameCsv.SourceGen;

/// <summary>
/// Contains attribute data from a property, field, or parameter.
/// </summary>
internal readonly struct SymbolMetadata
{
    public ISymbol Symbol { get; }
    public string[] Names { get; }
    public bool IsRequired { get; }
    public int Order { get; }
    public CsvBindingScope Scope { get; }

    public SymbolMetadata(ISymbol token, ISymbol symbol, FlameSymbols flameSymbols)
    {
        Symbol = symbol;

        foreach (var attributeData in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, flameSymbols.CsvHeaderAttribute))
            {
                // params-array
                if (attributeData.ConstructorArguments[0].Values is { IsDefaultOrEmpty: false } namesArray)
                {
                    var names = new string[namesArray.Length];

                    for (int i = 0; i < namesArray.Length; i++)
                        names[i] = namesArray[i].Value?.ToString() ?? "";

                    Names = names;
                }

                foreach (var argument in attributeData.NamedArguments)
                {
                    switch (argument)
                    {
                        case { Key: "Required", Value.Value: bool requiredArg }:
                            IsRequired = requiredArg;
                            break;
                        case { Key: "Order", Value.Value: int orderArg }:
                            Order = orderArg;
                            break;
                        case { Key: "Scope", Value.Value: CsvBindingScope scopeArg }:
                            Scope = scopeArg;
                            break;
                    }
                }
            }
            else if (attributeData.AttributeClass is { IsGenericType: true } attribute &&
                     SymbolEqualityComparer.Default.Equals(token, attribute.TypeArguments[0]) &&
                     SymbolEqualityComparer.Default.Equals(
                         attribute.ConstructUnboundGenericType(),
                         flameSymbols.CsvConverterOfTAttribute))
            {
            }
        }

        Names ??= [symbol.Name];
    }
}
