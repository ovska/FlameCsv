using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen;

/// <summary>
/// Contains attribute data from a property, field, or parameter.
/// </summary>
// ref struct to avoid accidental storage
internal readonly ref struct SymbolMetadata
{
    public EquatableArray<string> Names { get; }
    public bool IsRequired { get; }
    public bool IsIgnored { get; }
    public int Order { get; }
    public int? Index { get; }

    public Location? GetLocation(CancellationToken cancellationToken)
        => _attributeSyntax?.GetSyntax(cancellationToken).GetLocation();

    private readonly SyntaxReference? _attributeSyntax;

    public SymbolMetadata(ISymbol symbol, ref readonly FlameSymbols flameSymbols)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (!flameSymbols.IsCsvFieldAttribute(attributeData.AttributeClass))
            {
                continue;
            }

            _attributeSyntax = attributeData.ApplicationSyntaxReference;
            Names = attributeData.ConstructorArguments[0].Values.TryToEquatableStringArray();

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

            // [CsvField] has AllowMultiple = false
            break;
        }
    }
}
