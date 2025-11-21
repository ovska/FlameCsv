using CommunityToolkit.HighPerformance;
using FlameCsv.IO.Internal;
using FlameCsv.Reading;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public class ParallelReaderTests
{
    [Fact]
    public void Should_Read()
    {
        TestConsoleWriter.RedirectToTestOutput();

        ReadOnlyMemory<byte> data = TestDataGenerator.GenerateBytes(CsvNewline.CRLF, true, true, Escaping.None);

        Assert.Equal(CsvReader.Read<Obj>(data), ReadSequential());

        IEnumerable<Obj> ReadSequential()
        {
            using var pool = ReturnTrackingMemoryPool<char>.Create();
            CsvOptions<char> options = new() { MemoryPool = pool };

            using var sr = new StreamReader(data.AsStream());
            using var reader = new ParallelTextReader(sr, options, default);
            IMaterializer<char, Obj>? materializer = null;

            while (reader.Read() is Chunk<char> chunk)
            {
                while (chunk.TryPop(out CsvRecordRef<char> record))
                {
                    if (materializer is null)
                    {
                        List<string> headers = [];

                        for (int i = 0; i < record.FieldCount; i++)
                        {
                            headers.Add(record[i].ToString());
                        }

                        materializer = options.TypeBinder.GetMaterializer<Obj>([.. headers]);
                    }
                    else
                    {
                        var obj = materializer.Parse(ref record);
                        yield return obj;
                    }
                }

                chunk.Dispose();
            }
        }
    }
}
