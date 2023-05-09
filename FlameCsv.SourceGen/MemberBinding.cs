using System.Collections.Immutable;

namespace FlameCsv.SourceGen;

internal interface IBinding
{
     string Name { get; }
     IEnumerable<string> Names { get; }
     ISymbol Symbol { get; }
     ITypeSymbol Type { get; }
     bool IsRequired { get; }
     string ParserId { get; }
     string HandlerId { get; }
     int Order { get; }
}

internal sealed class TypeBindings
{
    public ImmutableArray<MemberBinding> Members { get; }
    public ImmutableArray<MemberBinding> RequiredMembers { get; }

    public ImmutableArray<ParameterBinding> Parameters { get; }

    public ImmutableArray<IBinding> AllBindings { get; }
    public ImmutableArray<IBinding> RequiredBindings { get; }

    public TypeBindings(ImmutableArray<MemberBinding> members, ImmutableArray<ParameterBinding> parameters)
    {
        Members = members;
        Parameters = parameters;

        var builder = ImmutableArray.CreateBuilder<IBinding>(members.Length + parameters.Length);
        builder.AddRange(Members);
        builder.AddRange(Parameters);
        AllBindings = builder.MoveToImmutable();

        RequiredMembers = Members.Where(x => x.IsRequired).ToImmutableArray();
        RequiredBindings = AllBindings.Where(x => x.IsRequired).ToImmutableArray();
    }
}

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
        IsRequired = isRequired || symbol is IPropertySymbol{ SetMethod.IsInitOnly: true };
        Order = order;
        Names = names;
        ParserId = $"@__Parser_{symbol.Name}";
        HandlerId = $"@s__Handler_{symbol.Name}";
    }

    public int CompareTo(MemberBinding other) => other.Order.CompareTo(Order); // reverse sort so higher order is first
}
