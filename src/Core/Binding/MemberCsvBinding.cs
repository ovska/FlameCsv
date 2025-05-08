using System.Reflection;
using FlameCsv.Reflection;
using FlameCsv.Utilities;

namespace FlameCsv.Binding;

internal sealed class MemberCsvBinding<T>(int index, MemberData member, string header) : CsvBinding<T>(index, header)
{
    public MemberCsvBinding(int index, MemberData member) : this(index, member, member.Value.Name)
    {
    }

    public MemberInfo Member => member.Value;

    public override Type Type => member.MemberType;
    public override Type? DeclaringType => member.Value.DeclaringType;

    protected override MemberInfo Sentinel => member.Value;
    protected override ReadOnlySpan<object> Attributes => member.Attributes;

    private protected override void PrintDetails(ref ValueStringBuilder vsb)
    {
        vsb.Append(member.IsProperty ? "Property: " : "Field: ");
        vsb.Append(Type.Name);
        vsb.Append(' ');
        vsb.Append(member.Value.Name);
    }

    protected internal override string DisplayName
        => $"{member.Value.Name} ({(member.IsProperty ? "Property" : "Field")})";
}
