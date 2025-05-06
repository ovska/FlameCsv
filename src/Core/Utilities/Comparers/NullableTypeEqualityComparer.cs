namespace FlameCsv.Utilities.Comparers;

internal sealed class NullableTypeEqualityComparer : IEqualityComparer<Type>
{
    public static readonly NullableTypeEqualityComparer Instance = new();

    private NullableTypeEqualityComparer() { }

    public bool Equals(Type? x, Type? y)
    {
        if (x == y)
            return true;

        if (x is null || y is null)
            return false;

        return (Nullable.GetUnderlyingType(x) ?? x) == (Nullable.GetUnderlyingType(y) ?? y);
    }

    public int GetHashCode(Type obj)
    {
        return (Nullable.GetUnderlyingType(obj) ?? obj).GetHashCode();
    }
}
