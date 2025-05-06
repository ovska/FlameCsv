using System.Runtime.CompilerServices;

namespace FlameCsv.Tests;

public static class TestingExtensions
{
    /// <summary>
    /// Returns either "\n" or "\r\n" depending on the newline type.
    /// </summary>
    public static string AsString(this CsvNewline newline)
    {
        return newline switch
        {
            CsvNewline.LF => "\n",
            CsvNewline.Platform => Environment.NewLine,
            _ => "\r\n",
        };
    }

    public static ConfiguredCancelableAsyncEnumerable<T> WithTestContext<T>(this IAsyncEnumerable<T> source)
    {
        return source.ConfigureAwait(false).WithCancellation(TestContext.Current.CancellationToken);
    }
}
