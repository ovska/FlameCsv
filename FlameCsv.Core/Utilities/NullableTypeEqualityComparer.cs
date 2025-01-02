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

        return (Nullable.GetUnderlyingType(x) ?? x).Equals(Nullable.GetUnderlyingType(y) ?? y);
    }

    public int GetHashCode([DisallowNull] Type obj)
    {
        return (Nullable.GetUnderlyingType(obj) ?? obj).GetHashCode();
    }
}
