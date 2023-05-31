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

        var builder = ImmutableArray.CreateBuilder<IBinding>(members.Length + parameters.Length);
        builder.AddRange(Members);
        builder.AddRange(Parameters);
        AllBindings = builder.MoveToImmutable();

        RequiredMembers = Members.Where(x => x.IsRequired).ToImmutableArray();
        RequiredBindings = AllBindings.Where(x => x.IsRequired).ToImmutableArray();
    }
}
