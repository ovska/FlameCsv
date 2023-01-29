namespace FlameCsv.Tests.Utilities;

internal static class TestingExtensions
{
    public static IEnumerable<IList<T>> GetPermutations<T>(this IList<T> collection)
    {
        return Iter(collection, 0, collection.Count - 1);

        static IEnumerable<IList<T>> Iter(IList<T> list, int start, int end)
        {
            if (start == end)
            {
                yield return list;
            }
            else
            {
                for (var i = start; i <= end; i++)
                {
                    Swap(start, i);

                    foreach (var inner in Iter(list, start + 1, end))
                        yield return inner;

                    Swap(start, i);
                }
            }

            void Swap(int a, int b)
            {
                (list[a], list[b]) = (list[b], list[a]);
            }
        }
    }
}
