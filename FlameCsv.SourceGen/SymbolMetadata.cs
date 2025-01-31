namespace FlameCsv.SourceGen;

/// <summary>
/// Contains attribute data from a property, field, or parameter.
/// </summary>
internal readonly struct SymbolMetadata
{
    public ISymbol Symbol { get; }
    public string[] Names { get; }
    public bool IsRequired { get; }
    public bool IsIgnored { get; }
    public int Order { get; }
    public int? Index { get; }

    public SymbolMetadata(ISymbol symbol, FlameSymbols flameSymbols)
    {
        Symbol = symbol;

        foreach (var attributeData in symbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, flameSymbols.CsvFieldAttribute))
            {
                continue;
            }

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
                switch (argument.Key)
                {
                    case "IsIgnored":
                        IsIgnored = argument.Value.Value is true;
                        break;
                    case "IsRequired":
                        IsRequired = argument.Value.Value is true;
                        break;
                    case "Order":
                        Order = argument.Value.Value as int? ?? 0;
                        break;
                    case "Index":
                        Index = argument.Value.Value as int?;
                        if (Index < 0) Index = null;
                        break;
                }
            }
        }

        Names ??= [symbol.Name];
    }
}
