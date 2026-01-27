using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Exceptions;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Reading;

public class ParallelReaderTests
{
    [Fact]
    public void Should_Skip_Utf8_Preamble()
    {
        using MemoryStream ms = new();
        using (StreamWriter writer = new(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true))
        {
            writer.Write("Hello\nWorld!");
        }

        ms.Position = 0;

        var results = Csv.From(ms)
            .AsParallel()
            .Read<ValueTuple<string>>(new() { HasHeader = false })
            .SelectMany(t => t)
            .Select(t => t.Item1)
            .ToList();

        Assert.Equal(["Hello", "World!"], results);
    }

    [Fact]
    public async Task Should_Throw_On_Ordered_ToChannel()
    {
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, false);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            Channel<Foo> channel = Channel.CreateUnbounded<Foo>();
            await Csv.From(data)
                .AsParallel(new() { Unordered = false, CancellationToken = TestContext.Current.CancellationToken })
                .ToChannelAsync(channel.Writer);
        });
    }

    [Fact]
    public void Should_Read_Sync()
    {
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, false);

        Assert.Equal(
            Csv.From(data).Read<Obj>(),
            Csv.From(data).AsParallel().Read(ObjCharTypeMap.Default).SelectMany(s => s),
            EqualityComparer<Obj>.Default
        );
    }

    [Fact]
    public async Task Should_Read()
    {
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, false);

        List<Obj> list = [];

        await foreach (
            var obj in Csv.From(data)
                .AsParallel(new() { CancellationToken = TestContext.Current.CancellationToken, Unordered = true })
                .ReadAsync<Obj>(ObjCharTypeMap.Default)
        )
        {
            lock (list)
            {
                list.AddRange(obj);
            }
        }

        list.Sort();
        Assert.Equal(Csv.From(data).Read<Obj>(), list, EqualityComparer<Obj>.Default);
    }

    [Fact]
    public async Task Should_ForEach()
    {
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, false);

        ConcurrentBag<Obj> bag = new();

        await Csv.From(data)
            .AsParallel()
            .ForEachAsync(
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

        Assert.Equal(Csv.From(data).Read<Obj>(), bag.OrderBy(o => o.Id), EqualityComparer<Obj>.Default);
    }

    [Fact]
    public async Task Should_Read_To_Channel()
    {
        string data = TestDataGenerator.GenerateText(CsvNewline.CRLF, true, false);

        Channel<Obj> channel = Channel.CreateUnbounded<Obj>(new() { SingleReader = true });
        List<Obj> list = [];

        Task task = Csv.From(data)
            .AsParallel(new() { Unordered = true, CancellationToken = TestContext.Current.CancellationToken })
            .ToChannelAsync(channel.Writer);

        await foreach (var item in channel.Reader.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            list.Add(item);
        }

        await task;

        list.Sort();

        Assert.Equal(Csv.From(data).Read<Obj>(), list, EqualityComparer<Obj>.Default);
    }

    [Fact]
    public async Task Should_Rethrow_Exceptions_On_Parse()
    {
        using var apbw = new ArrayPoolBufferWriter<byte>();

        long invalidPosition = -1;
        apbw.Write("name,id,value\n"u8);

        for (int i = 0; i < 256; i++)
        {
            if (i == 123)
            {
                invalidPosition = apbw.WrittenCount;
                apbw.Write("invalid,row,data\n"u8);
            }
            else
            {
                apbw.Write("test,1,a\n"u8);
            }
        }

        var builder = Csv.From(apbw.WrittenMemory)
            .AsParallel(new() { CancellationToken = TestContext.Current.CancellationToken, Unordered = true });

        var ex = Assert.Throws<CsvParseException>(() =>
        {
            foreach (var c in builder.Read<Foo>())
            {
                AssertChunk(c);
            }
        });
        AssertEx();

        ex = Assert.Throws<CsvParseException>(() =>
        {
            builder.ForEach<Foo>(AssertChunk);
        });
        AssertEx();

        ex = await Assert.ThrowsAsync<CsvParseException>(async () =>
        {
            await foreach (var c in builder.ReadAsync<Foo>().WithTestContext())
            {
                AssertChunk(c);
            }
        });
        AssertEx();

        ex = await Assert.ThrowsAsync<CsvParseException>(async () =>
        {
            await builder.ForEachAsync<Foo>(
                (c, _) =>
                {
                    AssertChunk(c);
                    return ValueTask.CompletedTask;
                }
            );
        });
        AssertEx();

        foreach (
            var channel in (Channel<Foo>[])
                [
                    Channel.CreateUnbounded<Foo>(),
                    Channel.CreateBounded<Foo>(
                        new BoundedChannelOptions(5) { FullMode = BoundedChannelFullMode.DropWrite }
                    ),
                ]
        )
        {
            ex = await Assert.ThrowsAsync<CsvParseException>(async () =>
            {
                Task task = builder.ToChannelAsync(channel.Writer);
                await foreach (var f in channel.Reader.ReadAllAsync(TestContext.Current.CancellationToken))
                {
                    AssertSingle(f);
                }
                await task;
            });
            AssertEx();
        }

        static void AssertChunk(ArraySegment<Foo> chunk)
        {
            Assert.All(chunk, AssertSingle);
        }

        static void AssertSingle(Foo item)
        {
            Assert.Equal(1, item.Id);
            Assert.Equal("test", item.Name);
            Assert.Equal('a', item.Value);
        }

        void AssertEx()
        {
            Assert.Equal(125, ex.Line);
            Assert.Equal(invalidPosition, ex.RecordPosition);
            Assert.Equal(invalidPosition + 8, ex.FieldPosition);
            Assert.Equal("invalid,row,data", ex.RecordValue);
            Assert.Equal("id", ex.HeaderValue);
            Assert.Equal(1, ex.FieldIndex);
        }
    }

    private record Foo(int Id, string Name, char Value);
}
