using CommunityToolkit.HighPerformance;
using FlameCsv.IO.Internal;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Reading;

public class ParallelReaderTests
{
    [Fact]
    public void Should_Read()
    {
        Assert.Equal(TestDataGenerator.Objects.Value, ReadSequential());

        IEnumerable<Obj> ReadSequential()
        {
            var data = TestDataGenerator.GenerateBytes(CsvNewline.CRLF, true, true, Escaping.None);

            using var sr = new StreamReader(data.AsStream());
            using var reader = new ParallelTextReader(sr, CsvOptions<char>.Default, default);
            IMaterializer<char, Obj>? materializer = null;

            while (reader.Read() is Chunk<char> chunk)
            {
                while (chunk.TryPop(out var record))
                {
                    if (materializer is null)
                    {
                        List<string> headers = [];

                        for (int i = 0; i < record.FieldCount; i++)
                        {
                            headers.Add(record[i].ToString());
                        }

                        materializer = CsvOptions<char>.Default.TypeBinder.GetMaterializer<Obj>([.. headers]);
                    }
                    else
                    {
                        var obj = materializer.Parse(ref record);
                        obj.Name = $" {obj.Name?.Replace('-', '\'')}";
                        obj.LastLogin = DateTimeOffset.UnixEpoch;
                        yield return obj;
                    }
                }
                chunk.Dispose();
            }
        }
    }
}
