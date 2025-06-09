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

        var sb = new StringBuilder();

        using (var writer = new CsvFieldWriter<char>(new StringBuilderBufferWriter(sb, pool), CsvOptions<char>.Default))
        {
            writeAction(writer);
        }

        return sb.ToString();
    }
}
