
namespace FlameCsv.Binding;

internal readonly record struct HeaderBindingCandidate(string Value, object Target, int Order)
    : IComparable<HeaderBindingCandidate>
{
    public int CompareTo(HeaderBindingCandidate other) => Order.CompareTo(other.Order);
}
