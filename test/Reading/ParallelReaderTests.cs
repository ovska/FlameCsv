using System.Collections.Concurrent;
using CommunityToolkit.HighPerformance;
using FlameCsv.IO.Internal;
using FlameCsv.ParallelUtils;
using FlameCsv.Reading;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public class ParallelReaderTests
{
    [Fact]
    public void Should_Read()
    {
        TestConsoleWriter.RedirectToTestOutput();

        ReadOnlyMemory<byte> data = TestDataGenerator.GenerateBytes(CsvNewline.CRLF, true, Escaping.None);

        Assert.Equal(Csv.From(data).Read<Obj>(), ReadSequential());

        IEnumerable<Obj> ReadSequential()
        {
            using var pool = new ReturnTrackingBufferPool();

            using var sr = new StreamReader(data.AsStream());
            using var reader = new ParallelTextReader(sr, CsvOptions<char>.Default, new() { BufferPool = pool });
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

                        materializer = CsvOptions<char>.Default.TypeBinder.GetMaterializer<Obj>([.. headers]);
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

    [Fact]
    public void Should_Read_2()
    {
        TestConsoleWriter.RedirectToTestOutput();

        ReadOnlyMemory<char> data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, Escaping.None);

        List<Obj> list = [];

        foreach (var span in Csv.From(data).AsParallel().ReadUnordered(ObjCharTypeMap.Default))
        {
            foreach (var item in span)
            {
                list.Add(item);
            }
        }

        list.Sort();

        Assert.Equal(Csv.From(data).Read<Obj>(), list);
    }

    [Fact]
    public async Task Should_Read_Async()
    {
        TestConsoleWriter.RedirectToTestOutput();

        ReadOnlyMemory<char> data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, Escaping.None);

        List<Obj> list = [];

        await foreach (
            var obj in Csv.From(data)
                .AsParallel()
                .ReadUnorderedAsync<Obj>(ObjCharTypeMap.Default)
                .WithCancellation(TestContext.Current.CancellationToken)
        )
        {
            foreach (var item in obj)
            {
                list.Add(item);
            }
        }

        list.Sort();

        Assert.Equal(Csv.From(data).Read<Obj>(), list);
    }

    [Fact]
    public async Task Should_Read_Async_2()
    {
        TestConsoleWriter.RedirectToTestOutput();

        ReadOnlyMemory<char> data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, Escaping.None);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        ConcurrentBag<Obj> bag = new();

        await Csv.From(data)
            .AsParallel()
            .ForEachUnorderedAsync(
                ObjCharTypeMap.Default,
                (values, ct) =>
                {
                    foreach (var item in values)
                    {
                        bag.Add(item);
                    }
                    return ValueTask.CompletedTask;
                }
            );

        List<Obj> list = bag.OrderBy(o => o.Id).ToList();

        var expected = Csv.From(data).Read<Obj>().ToList();

        Assert.Empty(expected.Except(list).Reverse());
    }
}
