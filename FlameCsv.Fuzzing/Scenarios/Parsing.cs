using System.Buffers;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Fuzzing.Scenarios;

public class Parsing : IScenario
{
    public static void Run(ReadOnlyMemory<byte> data, PoisonPagePlacement placement)
    {
        try
        {
            using var pool = new BoundedMemoryPool<byte>();
            using var stream = data.AsStream();

            var options = new CsvOptions<byte> { MemoryPool = pool };
            var readOptions = new CsvReaderOptions { NoDirectBufferAccess = true };

            foreach (var r in CsvParser.Create(options, CsvPipeReader.Create(stream, pool, readOptions)).ParseRecords())
            {
                for (int i = 0; i < r.FieldCount; i++)
                {
                    _ = r[i];
                }
            }
        }
        catch (CsvFormatException)
        {
            // invalid CSV produces this exception
        }
    }

    public static bool SupportsUtf16 => true;

    public static void Run(ReadOnlyMemory<char> data, PoisonPagePlacement placement)
    {
        try
        {
            using var pool = new BoundedMemoryPool<char>();
            var options = new CsvOptions<char> { MemoryPool = pool };

            foreach (var r in CsvParser
                         .Create(options, CsvPipeReader.Create(new ReadOnlySequence<char>(data)))
                         .ParseRecords())
            {
                for (int i = 0; i < r.FieldCount; i++)
                {
                    _ = r[i];
                }
            }
        }
        catch (CsvFormatException)
        {
            // invalid CSV produces this exception
        }
    }
}
