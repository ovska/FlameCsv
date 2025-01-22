
namespace FlameCsv.SourceGen.Bindings;

#pragma warning disable IDE0290 // Use primary constructor

internal sealed class ParameterBinding : IComparable<ParameterBinding>, IBinding
{
    public string Name { get; }
    public string ParameterName => Symbol.Name;
    public IReadOnlyList<string> Names { get; }
    public BindingScope Scope => BindingScope.Read;

    ISymbol IBinding.Symbol => Symbol;
    public IParameterSymbol Symbol { get; }
    public ITypeSymbol Type => Symbol.Type;
    public int Order { get; }
    public bool IsRequired { get; }
    public int ParameterPosition => Symbol.Ordinal;
    public bool HasInModifier => Symbol.RefKind is RefKind.In or RefKind.RefReadOnlyParameter;
    public object? DefaultValue => Symbol.ExplicitDefaultValue;

    public bool CanRead => true;
    public bool CanWrite => false;

    public ParameterBinding(
        IParameterSymbol symbol,
        in SymbolMetadata meta)
    {
        Name = $"@p_{symbol.Name}";
        Symbol = symbol;
        Order = meta.Order;
        IsRequired = meta.IsRequired || !symbol.HasExplicitDefaultValue;
        Names = meta.Names;
    }

    public int CompareTo(ParameterBinding other) => other.Order.CompareTo(Order); // reverse sort so higher order is first

    public void WriteConverterId(StringBuilder sb)
    {
        sb.Append("@__Converter_p_");
        sb.Append(Symbol.Name);
    }

    public void WriteHandlerId(StringBuilder sb)
    {
        sb.Append("@s__Handler_p_");
        sb.Append(Symbol.Name);
    }

    public void WriteIndex(StringBuilder sb, int? index = null)
    {
        _index ??= index ?? throw new InvalidOperationException();

        sb.Append("@s__Index_p_");
        sb.Append(Symbol.Name);
    }

    public int Index => _index ?? throw new InvalidOperationException();

    private int? _index;
}
