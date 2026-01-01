using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Exceptions;
using FlameCsv.IO.Internal;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public class ParallelReaderTests
{
    [Fact]
    public void Should_Read()
    {
        ReadOnlyMemory<byte> data = TestDataGenerator.GenerateBytes(CsvNewline.CRLF, true, hasQuotes: false);

        Assert.Equal(Csv.From(data).Read<Obj>(), ReadSequential());

        IEnumerable<Obj> ReadSequential()
        {
            using var pool = new ReturnTrackingBufferPool();

            using var sr = new StreamReader(data.AsStream());
            using var reader = new ParallelTextReader(sr, CsvOptions<char>.Default, new() { BufferPool = pool });
            IMaterializer<char, Obj>? materializer = null;

            while (reader.Read() is Chunk<char> chunk)
            {
                while (chunk.RecordBuffer.TryPop(out RecordView view))
                {
                    CsvRecordRef<char> record = new(chunk, ref MemoryMarshal.GetReference(chunk.Data.Span), view);

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
                        var obj = materializer.Parse(record);
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
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, false);

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
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, false);

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
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, false);
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

    [Fact]
    public async Task Should_Rethrow_Exceptions_On_Parse()
    {
        using var apbw = new ArrayPoolBufferWriter<byte>();

        apbw.Write("id,name,value\n"u8);

        for (int i = 0; i < 256; i++)
        {
            if (i == 123)
            {
                apbw.Write("invalid,row,data\n"u8);
            }
            else
            {
                apbw.Write("1,test,a\n"u8);
            }
        }

        var builder = Csv.From(apbw.WrittenMemory).AsParallel(TestContext.Current.CancellationToken);

        Assert.Throws<CsvParseException>(() =>
        {
            foreach (var _ in builder.ReadUnordered<Foo>()) { }
        });

        await Assert.ThrowsAsync<CsvParseException>(async () =>
        {
            await foreach (var _ in builder.ReadUnorderedAsync<Foo>().WithTestContext()) { }
        });

        await Assert.ThrowsAsync<CsvParseException>(async () =>
        {
            Channel<Foo> channel = Channel.CreateUnbounded<Foo>();
            Task task = builder.WriteToChannelAsync(channel.Writer);
            await foreach (var _ in channel.Reader.ReadAllAsync(TestContext.Current.CancellationToken)) { }
            await task;
        });
    }

    private record Foo(int Id, string Name, char Value);
}
