namespace FlameCsv.SourceGen.Bindings;

internal readonly struct ParameterBinding : IComparable<ParameterBinding>, IBinding
{
    public string Name { get; }
    public string ParameterName => Symbol.Name;
    public IEnumerable<string> Names { get; }

    ISymbol IBinding.Symbol => Symbol;
    public IParameterSymbol Symbol { get; }
    public ITypeSymbol Type { get; }
    public string ParserId { get; }
    public string HandlerId { get; }
    public int Order { get; }
    public bool IsRequired { get; }
    public int ParameterPosition { get; }
    public bool HasInModifier => Symbol.RefKind == RefKind.In;
    public object? DefaultValue => Symbol.ExplicitDefaultValue;

    public ParameterBinding(
        IParameterSymbol symbol,
        ITypeSymbol type,
        int order,
        bool explicitRequired,
        IEnumerable<string> names)
    {
        Symbol = symbol;
        Type = type;
        Order = order;
        Names = names;
        Name = $"@p_{symbol.Name}";
        ParserId = $"@__Parser_p_{symbol.Name}";
        HandlerId = $"@s__Handler_p_{symbol.Name}";
        ParameterPosition = symbol.Ordinal;
        IsRequired = explicitRequired || !symbol.HasExplicitDefaultValue;
    }

    public int CompareTo(ParameterBinding other) => other.Order.CompareTo(Order); // reverse sort so higher order is first
}
