using System.Collections.Immutable;

namespace FlameCsv.SourceGen;

internal static class Extensions
{
    public static T FindValueOrDefault<T>(this ImmutableArray<T> array, Func<T, bool> predicate)
    {
        foreach (var item in array)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return default!;
    }
}
