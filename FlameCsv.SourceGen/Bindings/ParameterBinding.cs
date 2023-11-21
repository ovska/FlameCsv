namespace FlameCsv.SourceGen.Bindings;

internal readonly struct ParameterBinding(
    IParameterSymbol symbol,
    ITypeSymbol type,
    int order,
    bool explicitRequired,
    IEnumerable<string> names) : IComparable<ParameterBinding>, IBinding
{
    public string Name { get; } = $"@p_{symbol.Name}";
    public string ParameterName => symbol.Name;
    public IEnumerable<string> Names => names;

    ISymbol IBinding.Symbol => symbol;
    public IParameterSymbol Symbol => symbol;
    public ITypeSymbol Type => type;
    public string ParserId { get; } = $"@__Parser_p_{symbol.Name}";
    public string HandlerId { get; } = $"@s__Handler_p_{symbol.Name}";
    public int Order => order;
    public bool IsRequired => explicitRequired || !symbol.HasExplicitDefaultValue;
    public int ParameterPosition => symbol.Ordinal;
    public bool HasInModifier => symbol.RefKind == RefKind.In;
    public object? DefaultValue => symbol.ExplicitDefaultValue;

    public int CompareTo(ParameterBinding other) => other.Order.CompareTo(Order); // reverse sort so higher order is first
}
