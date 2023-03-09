using System.Reflection;
using System.Text;
using FlameCsv.Reflection;

namespace FlameCsv.Binding.Internal;

internal sealed class MemberCsvBinding<T> : CsvBinding<T>
{
    public MemberInfo Member => _member.Value;

    public override Type Type => _member.MemberType;

    protected override object Sentinel => _member.Value;
    protected override ReadOnlySpan<object> Attributes => _member.Attributes;

    private readonly MemberData _member;

    public MemberCsvBinding(int index, MemberData member) : base(index)
    {
        _member = member;
    }

    protected override void PrintDetails(StringBuilder sb)
    {
        sb.Append(_member.IsProperty ? "Property: " : "Field: ");
        sb.Append(Type.Name);
        sb.Append(' ');
        sb.Append(_member.Value.Name);
    }
}
