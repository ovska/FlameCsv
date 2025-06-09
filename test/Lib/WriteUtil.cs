using System.Text;
using FlameCsv.IO.Internal;
using FlameCsv.Writing;

namespace FlameCsv.Tests;

public static class WriteUtil
{
    /// <summary>
    /// Returns a string representation of the given value.
    /// </summary>
    public static string Write(Action<CsvFieldWriter<char>> writeAction)
    {
        using var pool = ReturnTrackingMemoryPool<char>.Create(null);
        pool.TrackStackTraces = true;

        var sb = new StringBuilder();

        var inner = new StringBuilderBufferWriter(sb, pool);
        using (var writer = new CsvFieldWriter<char>(inner, CsvOptions<char>.Default))
        {
            writeAction(writer);
        }

        inner.Complete(null);
        return sb.ToString();
    }
}
