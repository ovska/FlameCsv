
namespace FlameCsv.SourceGen.Bindings;

internal sealed class MemberBinding : IComparable<MemberBinding>, IBinding
{
    public string Name => Symbol.Name;
    public IReadOnlyList<string> Names { get; }

    public ISymbol Symbol { get; }
    public ITypeSymbol Type { get; }
    public bool IsRequired { get; }
    public int Order { get; }
    public BindingScope Scope { get; set; }

    public bool CanRead { get; }
    public bool CanWrite { get; }

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

        CanRead = meta.Scope != BindingScope.Write &&
            symbol is IPropertySymbol { IsReadOnly: false } or IFieldSymbol { IsReadOnly: false, IsConst: false };
        CanWrite = meta.Scope != BindingScope.Read && symbol is not IPropertySymbol { IsWriteOnly: true };
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

    public void WriteConverterId(StringBuilder sb)
    {
        sb.Append("@__Converter_");
        sb.Append(Symbol.Name);
    }

    public void WriteHandlerId(StringBuilder sb)
    {
        sb.Append("@s__Handler_");
        sb.Append(Symbol.Name);
    }
}
