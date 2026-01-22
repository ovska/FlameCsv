using CommunityToolkit.HighPerformance;

namespace FlameCsv.Fuzzing.Scenarios;

public class Parsing : IScenario
{
    public static void Run(ReadOnlyMemory<byte> data, PoisonPagePlacement placement)
    {
        try
        {
            using var stream = data.AsStream();

            var readOptions = new CsvIOOptions
            {
                DisableOptimizations = true,
                BufferPool = new BoundedBufferPool(placement),
            };

            foreach (
                var r in new CsvReader<byte>(
                    CsvOptions<byte>.Default,
                    CsvBufferReader.Create(stream, readOptions),
                    in readOptions
                ).ParseRecords()
            )
            {
                for (int i = 0; i < r.FieldCount; i++)
                {
                    _ = r[i];
                    _ = r.GetFieldUnsafe(i);
                    _ = r.GetRawSpan(i);
                }

                _ = r.Raw;
                _ = r.GetRecordLength();
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
            var readOptions = new CsvIOOptions
            {
                DisableOptimizations = true,
                BufferPool = new BoundedBufferPool(placement),
            };

            foreach (
                var r in new CsvReader<char>(
                    CsvOptions<char>.Default,
                    CsvBufferReader.Create(data),
                    in readOptions
                ).ParseRecords()
            )
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
