namespace FlameCsv.SourceGen.Bindings;

internal sealed class MemberBinding : IComparable<MemberBinding>, IBinding
{
    public string Name => Symbol.Name;
    public IEnumerable<string> Names { get; }

    public ISymbol Symbol { get; }
    public ITypeSymbol Type { get; }
    public bool IsRequired { get; }
    public string ParserId { get; }
    public string HandlerId { get; }
    public string FormatterId { get; }
    public int Order { get; }
    public BindingScope Scope { get; }

    public MemberBinding(
        ISymbol symbol,
        ITypeSymbol type,
        in SymbolMetadata meta)
    {
        Symbol = symbol;
        Type = type;
        IsRequired = meta.IsRequired
            || symbol is IPropertySymbol { IsRequired: true }
            || symbol is IFieldSymbol { IsRequired: true }
            || symbol is IPropertySymbol { SetMethod.IsInitOnly: true };
        Order = meta.Order;
        Names = meta.Names;
        Scope = meta.Scope;
        ParserId = $"@__Parser_{symbol.Name}";
        HandlerId = $"@s__Handler_{symbol.Name}";
        FormatterId = $"@__Formatter_{symbol.Name}";
    }

    public int CompareTo(MemberBinding other) => other.Order.CompareTo(Order); // reverse sort so higher order is first

    public bool IsExplicitInterfaceDefinition(
        ITypeSymbol expectedParent,
        out INamedTypeSymbol interfaceSymbol)
    {
        if (Symbol.ContainingType.TypeKind == TypeKind.Interface &&
            !SymbolEqualityComparer.Default.Equals(Symbol.ContainingType, expectedParent))
        {
            interfaceSymbol = Symbol.OriginalDefinition.ContainingType;
            return true;
        }

        interfaceSymbol = null!;
        return false;
    }
}
