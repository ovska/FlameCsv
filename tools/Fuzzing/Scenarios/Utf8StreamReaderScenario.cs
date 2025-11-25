using System.Text;
using System.Text.Unicode;
using CommunityToolkit.HighPerformance;
using FlameCsv.IO.Internal;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.Fuzzing.Scenarios;

[UsedImplicitly]
public class Utf8StreamReaderScenario : IScenario
{
    public static void Run(ReadOnlyMemory<byte> data, PoisonPagePlacement placement)
    {
        var task = RunAsync(data, placement);

        using var reader = new Utf8StreamReader(
            data.AsStream(),
            new() { BufferPool = new BoundedBufferPool(placement) }
        );

        using var vsb = new ValueStringBuilder(stackalloc char[128]);

        while (true)
        {
            var result = reader.Read();

            vsb.Append(result.Buffer.Span);
            reader.Advance(result.Buffer.Length);

            if (result.IsCompleted)
            {
                break;
            }
        }

        var fromAsync = task.GetAwaiter().GetResult();

        var fromSync = vsb.ToString();

        if (fromAsync != fromSync)
        {
            throw new InvalidOperationException("Async and sync results do not match");
        }

        if (Utf8.IsValid(data.Span) && Encoding.UTF8.GetString(data.Span) != fromSync)
        {
            throw new InvalidOperationException("Invalid UTF8 data");
        }
    }

    public static async Task<string> RunAsync(ReadOnlyMemory<byte> data, PoisonPagePlacement placement)
    {
        var reader = new Utf8StreamReader(data.AsStream(), new() { BufferPool = new BoundedBufferPool(placement) });

        await using var _ = reader.ConfigureAwait(false);

        var sb = new StringBuilder();

        while (true)
        {
            var result = await reader.ReadAsync().ConfigureAwait(false);

            sb.Append(result.Buffer.Span);
            reader.Advance(result.Buffer.Length);

            if (result.IsCompleted)
            {
                break;
            }
        }

        return sb.ToString();
    }
}
