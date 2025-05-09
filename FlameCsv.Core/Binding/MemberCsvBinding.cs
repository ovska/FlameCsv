﻿using System.Reflection;
using System.Text;
using FlameCsv.Reflection;

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

    protected override void PrintDetails(StringBuilder sb)
    {
        sb.Append(member.IsProperty ? "Property: " : "Field: ");
        sb.Append(Type.Name);
        sb.Append(' ');
        sb.Append(member.Value.Name);
    }

    protected internal override string DisplayName
        => $"{member.Value.Name} ({(member.IsProperty ? "Property" : "Field")})";
}
