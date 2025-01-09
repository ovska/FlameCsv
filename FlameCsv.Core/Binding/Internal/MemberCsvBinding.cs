using System.Reflection;
using System.Text;
using FlameCsv.Reflection;

namespace FlameCsv.Binding.Internal;

internal sealed class MemberCsvBinding<T>(int index, MemberData member, string header) : CsvBinding<T>(index, header)
{
    public MemberCsvBinding(int index, MemberData member) : this(index, member, member.Value.Name)
    {
    }

    public MemberInfo Member => member.Value;

    public override Type Type => member.MemberType;

    protected override object Sentinel => member.Value;
    protected internal override ReadOnlySpan<object> Attributes => member.Attributes;

    protected override void PrintDetails(StringBuilder sb)
    {
        sb.Append(member.IsProperty ? "Property: " : "Field: ");
        sb.Append(Type.Name);
        sb.Append(' ');
        sb.Append(member.Value.Name);
    }

    protected internal override string DisplayName
        => member.Value.DeclaringType?.Name is { Length: > 0 } name
            ? $"{name}_{member.Value.Name}"
            : member.Value.Name;
}
