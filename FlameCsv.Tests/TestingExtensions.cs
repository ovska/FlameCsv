using System.Runtime.CompilerServices;

namespace FlameCsv.Tests;

internal static class TestingExtensions
{
    public static ConfiguredCancelableAsyncEnumerable<T> WithTestContext<T>(this IAsyncEnumerable<T> source)
    {
        return source.ConfigureAwait(false).WithCancellation(TestContext.Current.CancellationToken);
    }
}
