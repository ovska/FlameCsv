using System.Collections.Concurrent;
using System.Runtime.InteropServices;
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

    [Fact]
    public void Should_Read_2()
    {
        TestConsoleWriter.RedirectToTestOutput();

        ReadOnlyMemory<char> data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, Escaping.None);

        List<Obj> list = [];

        foreach (var span in CsvParallel.Read<Obj>(data, ObjCharTypeMap.Default))
        {
            foreach (var item in span)
            {
                list.Add(item);
            }
        }

        list.Sort();

        Assert.Equal(CsvReader.Read<Obj>(data), list);
    }

    [Fact]
    public async Task Should_Read_Async()
    {
        TestConsoleWriter.RedirectToTestOutput();

        ReadOnlyMemory<char> data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, Escaping.None);

        List<Obj> list = [];

        await foreach (
            var obj in CsvParallel
                .ReadAsync<Obj>(data, ObjCharTypeMap.Default)
                .WithCancellation(TestContext.Current.CancellationToken)
        )
        {
            foreach (var item in obj.Span)
            {
                list.Add(item);
            }
        }

        list.Sort();

        Assert.Equal(CsvReader.Read<Obj>(data), list);
    }

    [Fact]
    public async Task Should_Read_Async_2()
    {
        TestConsoleWriter.RedirectToTestOutput();

        ReadOnlyMemory<char> data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, Escaping.None);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        ConcurrentBag<Obj> bag = new();

        await CsvParallel.ForEachAsync(
            ParallelReader.Create(data, CsvOptions<char>.Default),
            ValueProducer<char, Obj>.Create(
                ObjCharTypeMap.Default,
                options: CsvOptions<char>.Default,
                new CsvParallelOptions { CancellationToken = TestContext.Current.CancellationToken }
            ),
            (obj, ex, ct) =>
            {
                foreach (var item in obj.GetSpan())
                {
                    bag.Add(item);
                }

                return ValueTask.CompletedTask;
            },
            cts,
            null
        );

        List<Obj> list = bag.OrderBy(o => o.Id).ToList();

        var expected = CsvReader.Read<Obj>(data).ToList();

        Assert.Empty(expected.Except(list).Reverse());
    }
}
