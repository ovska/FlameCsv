using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Utilities;

internal sealed class NullableTypeEqualityComparer : IEqualityComparer<Type>
{
    public static readonly NullableTypeEqualityComparer Instance = new();

    private NullableTypeEqualityComparer() { }

    public bool Equals(Type? x, Type? y)
    {
        if (ReferenceEquals(x, y))
            return true;

        if (x is null || y is null)
            return false;

        if (x.IsValueType && y.IsValueType)
        {
            return ReferenceEquals(x, Nullable.GetUnderlyingType(y))
                || ReferenceEquals(Nullable.GetUnderlyingType(x), y);
        }

        return false;
    }

    public int GetHashCode([DisallowNull] Type obj)
    {
        if (obj.IsValueType && Nullable.GetUnderlyingType(obj) is { } underlying)
            return underlying.GetHashCode();

        return obj.GetHashCode();
    }
}
