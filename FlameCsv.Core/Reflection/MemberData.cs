using System.Reflection;

namespace FlameCsv.Reflection;

internal sealed class MemberData : ReflectionData
{
    public MemberInfo Value { get; }
    public Type MemberType { get; }
    public bool IsReadOnly { get; }
    public bool IsProperty { get; }
    public override ReadOnlySpan<object> Attributes => _attributes ??= Value.GetCustomAttributes(inherit: true);

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

    public static explicit operator MemberData(MemberInfo member) => new(member);
}
