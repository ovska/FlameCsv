using System.Buffers;
using FlameCsv.IO;

namespace FlameCsv.Tests;

public static class IOExtensions
{
    public static ReadOnlyMemory<T> ReadToBuffer<T>(this ICsvBufferReader<T> reader) where T : unmanaged
    {
        ArrayBufferWriter<T> buffer = new();

        CsvReadResult<T> result;

        do
        {
            result = reader.Read();
            buffer.Write(result.Buffer.Span);
            reader.Advance(result.Buffer.Length);
        } while (!result.IsCompleted);

        return buffer.WrittenMemory;
    }

    public static async ValueTask<ReadOnlyMemory<T>> ReadToBufferAsync<T>(this ICsvBufferReader<T> reader)
        where T : unmanaged
    {
        ArrayBufferWriter<T> buffer = new();

        CsvReadResult<T> result;

        do
        {
            result = await reader.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            buffer.Write(result.Buffer.Span);
            reader.Advance(result.Buffer.Length);
        } while (!result.IsCompleted);

        return buffer.WrittenMemory;
    }
}
