namespace FlameCsv.SourceGen.Bindings;

#pragma warning disable IDE0290 // Use primary constructor

internal sealed class ParameterBinding : IComparable<ParameterBinding>, IBinding
{
    public string Name { get; }
    public string ParameterName => Symbol.Name;
    public IEnumerable<string> Names { get; }
    public BindingScope Scope => BindingScope.Read;

    ISymbol IBinding.Symbol => Symbol;
    public IParameterSymbol Symbol { get; }
    public ITypeSymbol Type => Symbol.Type;
    public string ConverterId { get; }
    public string HandlerId { get; }
    public int Order { get; }
    public bool IsRequired { get; }
    public int ParameterPosition => Symbol.Ordinal;
    public bool HasInModifier => Symbol.RefKind is RefKind.In or RefKind.RefReadOnlyParameter;
    public object? DefaultValue => Symbol.ExplicitDefaultValue;

    public ParameterBinding(
        IParameterSymbol symbol,
        in SymbolMetadata meta)
    {
        Name = $"@p_{symbol.Name}";
        Symbol = symbol;
        ConverterId = $"@__Converter_p_{symbol.Name}";
        HandlerId = $"@s__Handler_p_{symbol.Name}";
        Order = meta.Order;
        IsRequired = meta.IsRequired || !symbol.HasExplicitDefaultValue;
        Names = meta.Names;
    }

    public int CompareTo(ParameterBinding other) => other.Order.CompareTo(Order); // reverse sort so higher order is first
}
