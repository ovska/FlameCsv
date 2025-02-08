using System.Collections.Immutable;
using System.Reflection;

namespace FlameCsv.Binding.Internal;

internal readonly struct BindingData : IComparable<BindingData>
{
    public required string Name { get; init; }
    public required ICustomAttributeProvider Target { get; init; }
    public required ImmutableArray<string> Aliases { get; init; }
    public int Order { get; init; }
    public bool Ignored { get; init; }
    public bool Required { get; init; }
    public int? Index { get; init; }

    public int CompareTo(BindingData other)
    {
        int cmp = Order.CompareTo(other.Order);
        if (cmp == 0) cmp = Required.CompareTo(other.Required);
        if (cmp == 0) cmp = other.Ignored.CompareTo(Ignored);
        if (cmp == 0) cmp = Comparer<int?>.Default.Compare(Index, other.Index);
        return cmp;
    }
}
