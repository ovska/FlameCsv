namespace FlameCsv.SourceGen;

internal readonly struct Binding : IComparable<Binding>
{
    public string Name => Symbol.Name;
    public IEnumerable<string> Names { get; }

    public ISymbol Symbol { get; }
    public ITypeSymbol MemberType { get; }
    public bool IsRequired { get; }
    public string ParserId { get; }
    public string HandlerId { get; }
    public int Order { get; }

    public Binding(
        ISymbol symbol,
        ITypeSymbol type,
        bool isRequired,
        int order,
        IEnumerable<string> names)
    {
        Symbol = symbol;
        MemberType = type;
        IsRequired = isRequired;
        Order = order;
        Names = names;
        ParserId = $"@__Parser_{Name}";
        HandlerId = $"@s__Handler_{Name}";
    }

    public int CompareTo(Binding other) => other.Order.CompareTo(Order); // reverse sort so higher order is first
}
