namespace FlameCsv.SourceGen.Bindings;

internal interface IBinding
{
    string Name { get; }
    IEnumerable<string> Names { get; }
    ISymbol Symbol { get; }
    ITypeSymbol Type { get; }
    bool IsRequired { get; }
    string ConverterId { get; }
    string HandlerId { get; }
    int Order { get; }
    BindingScope Scope { get; }
}
