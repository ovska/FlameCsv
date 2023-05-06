namespace FlameCsv.SourceGen;

internal readonly struct Binding : IComparable<Binding>
{
    public string Name => Symbol.Name;
    public IEnumerable<string> Names { get; }

    public ISymbol Symbol { get; }
    public ITypeSymbol MemberType { get; }
    public bool IsRequired { get; }
    public int Id { get; }
    public string IdName { get; }
    public int Order { get; }

    public Binding(
        ISymbol symbol,
        ITypeSymbol type,
        bool isRequired,
        int id,
        int order,
        IEnumerable<string> names)
    {
        Symbol = symbol;
        MemberType = type;
        IsRequired = isRequired;
        Id = id;
        Order = order;
        Names = names;
        IdName = $"@__Field_{Name}";
    }

    public int CompareTo(Binding other) => other.Id.CompareTo(Id); // reverse sort so higher order is first
}
