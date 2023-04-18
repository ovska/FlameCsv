using System.Reflection;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reflection;

internal sealed class MemberData
{
    public MemberInfo Value { get; }
    public Type MemberType { get; }
    public bool IsReadOnly { get; }
    public bool IsProperty { get; }
    public ReadOnlySpan<object> Attributes => _attributes ?? GetOrInitMemberAttributes();

    private object[]? _attributes;

    private MemberData(MemberInfo member)
    {
        Value = member;
        (IsProperty, MemberType, IsReadOnly) = member switch
        {
            PropertyInfo p => (true, p.PropertyType, !p.CanWrite),
            FieldInfo f => (false, f.FieldType, f.IsInitOnly),
            _ => throw new InvalidOperationException("Invalid member type"),
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private object[] GetOrInitMemberAttributes()
    {
        var attributes = Value.GetCustomAttributes(inherit: true);
        return Interlocked.CompareExchange(ref _attributes, attributes, null) ?? attributes;
    }

    public static explicit operator MemberData(MemberInfo member) => new(member);
}
