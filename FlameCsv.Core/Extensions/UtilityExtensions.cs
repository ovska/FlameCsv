namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    /// <summary>
    /// Returns the array, or the shared <see cref="Array.Empty{T}"/> if it is empty.
    /// </summary>
    public static T[] ForCache<T>(this T[] array)
        => array.Length != 0 ? array : Array.Empty<T>();
}
