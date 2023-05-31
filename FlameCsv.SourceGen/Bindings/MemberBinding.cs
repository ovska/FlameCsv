namespace FlameCsv.SourceGen.Bindings;

internal readonly struct MemberBinding : IComparable<MemberBinding>, IBinding
{
    public string Name => Symbol.Name;
    public IEnumerable<string> Names { get; }

    public ISymbol Symbol { get; }
    public ITypeSymbol Type { get; }
    public bool IsRequired { get; }
    public string ParserId { get; }
    public string HandlerId { get; }
    public int Order { get; }

    public MemberBinding(
        ISymbol symbol,
        ITypeSymbol type,
        bool isRequired,
        int order,
        IEnumerable<string> names)
    {
        Symbol = symbol;
        Type = type;
        IsRequired = isRequired
            || symbol is IPropertySymbol { IsRequired: true }
            || symbol is IFieldSymbol { IsRequired: true }
            || symbol is IPropertySymbol { SetMethod.IsInitOnly: true }; // TODO: read
        Order = order;
        Names = names;
        ParserId = $"@__Parser_{symbol.Name}";
        HandlerId = $"@s__Handler_{symbol.Name}";
    }

    public int CompareTo(MemberBinding other) => other.Order.CompareTo(Order); // reverse sort so higher order is first
}
