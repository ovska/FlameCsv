using System.Diagnostics;
using System.Reflection;

namespace FlameCsv.Binding;

internal readonly struct HeaderBindingCandidate : IComparable<HeaderBindingCandidate>
{
    public HeaderBindingCandidate(string value, MemberInfo target, int order, bool isRequired)
    {
        Debug.Assert(target is PropertyInfo or FieldInfo);
        Value = value;
        Target = target;
        Order = order;
        IsRequired = isRequired;
    }

    public HeaderBindingCandidate(string value, ParameterInfo target, int order, bool isRequired)
    {
        Value = value;
        Target = target;
        Order = order;
        IsRequired = isRequired || !target.HasDefaultValue;
    }

    public string Value { get; }
    public object Target { get; }
    public int Order { get; }
    public bool IsRequired { get; }

    public int CompareTo(HeaderBindingCandidate other) => Order.CompareTo(other.Order);
}
