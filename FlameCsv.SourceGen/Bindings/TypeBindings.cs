using System.Collections.Immutable;

namespace FlameCsv.SourceGen.Bindings;

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
        AllBindings = [.. Members, .. Parameters];

        RequiredMembers = [..Members.Where(x => x.IsRequired && x.Scope != BindingScope.Write)];
        RequiredBindings = [..AllBindings.Where(x => x.IsRequired && x.Scope != BindingScope.Write)];
    }
}

