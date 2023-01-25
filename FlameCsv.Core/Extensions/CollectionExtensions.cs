namespace FlameCsv.Extensions;

internal static class CollectionExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this IDictionary<TKey, TValue>? dictionary,
        TKey key)
    {
        if (dictionary is not null && dictionary.TryGetValue(key, out TValue? value))
            return value;

        return default;
    }
}
