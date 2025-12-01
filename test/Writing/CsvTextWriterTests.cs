using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Tests.TestData;
using FlameCsv.Utilities;
using Xunit.Sdk;

namespace FlameCsv.Tests.Writing;

public class CsvTextWriterTests : CsvWriterTestsBase
{
    [Theory, MemberData(nameof(Args))]
    public void Objects_Sync(
        CsvNewline newline,
        bool header,
        CsvFieldQuoting quoting,
        bool sourceGen,
        int bufferSize,
        bool outputType,
        bool parallel,
        bool? guarded
    )
    {
        using var pool = new ReturnTrackingBufferPool(guarded);
        CsvIOOptions ioOptions = new() { BufferSize = bufferSize, BufferPool = pool };

        var options = new CsvOptions<char>
        {
            Newline = newline,
            HasHeader = header,
            FieldQuoting = quoting,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
        };

        Csv.IWriteBuilder<char> builder = GetBuilder(
            outputType,
            ioOptions,
            out StringBuilder? sb,
            out ArrayPoolBufferWriter<byte>? bufferWriter
        );

        if (sourceGen)
        {
            if (parallel)
            {
                builder.AsParallel().WriteUnordered(ObjCharTypeMap.Default, TestDataGenerator.Objects.Value, options);
            }
            else
            {
                builder.Write(ObjCharTypeMap.Default, TestDataGenerator.Objects.Value, options);
            }
        }
        else
        {
            if (parallel)
            {
                builder.AsParallel().WriteUnordered(TestDataGenerator.Objects.Value, options);
            }
            else
            {
                builder.Write(TestDataGenerator.Objects.Value, options);
            }
        }

        if (sb is not null)
        {
            Validate(FromStringBuilder(sb), header, newline.IsCRLF(), quoting);
            StringBuilderPool.Value.Return(sb);
            return;
        }

        Assert.NotNull(bufferWriter);
        Validate(FromArrayPoolBufferWriter(bufferWriter), header, newline.IsCRLF(), quoting);
        bufferWriter.Dispose();
    }

    private static Csv.IWriteBuilder<char> GetBuilder(
        bool outputType,
        CsvIOOptions ioOptions,
        out StringBuilder? sb,
        out ArrayPoolBufferWriter<byte>? bufferWriter
    )
    {
        sb = null;
        bufferWriter = null;
        return outputType
            ? Csv.To(
                (bufferWriter = new ArrayPoolBufferWriter<byte>(initialCapacity: short.MaxValue * 4)).AsStream(),
                Encoding.UTF8,
                ioOptions
            )
            : Csv.To(new StringWriter(sb = StringBuilderPool.Value.Get()), ioOptions);
    }

    [Theory, MemberData(nameof(Args))]
    public async Task Objects_Async(
        CsvNewline newline,
        bool header,
        CsvFieldQuoting quoting,
        bool sourceGen,
        int bufferSize,
        bool outputType,
        bool parallel,
        bool? guarded
    )
    {
        using var pool = new ReturnTrackingBufferPool(guarded);
        CsvIOOptions ioOptions = new() { BufferSize = bufferSize, BufferPool = pool };

        var options = new CsvOptions<char>
        {
            Newline = newline,
            HasHeader = header,
            FieldQuoting = quoting,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
        };

        Csv.IWriteBuilder<char> builder = GetBuilder(
            outputType,
            ioOptions,
            out StringBuilder? sb,
            out ArrayPoolBufferWriter<byte>? bufferWriter
        );

        if (parallel)
        {
            if (sourceGen)
            {
                await builder
                    .AsParallel(TestContext.Current.CancellationToken)
                    .WriteUnorderedAsync(ObjCharTypeMap.Default, TestDataGenerator.Objects.Value, options);
            }
            else
            {
                await builder
                    .AsParallel(TestContext.Current.CancellationToken)
                    .WriteUnorderedAsync(TestDataGenerator.Objects.Value, options);
            }
        }
        else
        {
            if (sourceGen)
            {
                await builder.WriteAsync(
                    ObjCharTypeMap.Default,
                    TestDataGenerator.Objects.Value,
                    options,
                    TestContext.Current.CancellationToken
                );
            }
            else
            {
                await builder.WriteAsync(
                    TestDataGenerator.Objects.Value,
                    options,
                    TestContext.Current.CancellationToken
                );
            }
        }

        if (sb is not null)
        {
            Validate(FromStringBuilder(sb), header, newline.IsCRLF(), quoting);
            StringBuilderPool.Value.Return(sb);
            return;
        }

        Assert.NotNull(bufferWriter);
        Validate(FromArrayPoolBufferWriter(bufferWriter), header, newline.IsCRLF(), quoting);
        bufferWriter.Dispose();
    }

    [Fact]
    public static async Task Should_Write_Async_Enumerable()
    {
        await using StringWriter sw = new();
        await Csv.To(sw)
            .WriteAsync(
                SyncAsyncEnumerable.Create(
                    new
                    {
                        Id = 1,
                        Name = "Bob",
                        IsEnabled = true,
                    }
                ),
                cancellationToken: TestContext.Current.CancellationToken
            );

        Assert.Equal("Id,Name,IsEnabled\r\n1,Bob,true\r\n", sw.ToString());
    }

    private static void Validate(TextReader source, bool header, bool crlf, CsvFieldQuoting quoting)
    {
        char quote = quoting is CsvFieldQuoting.Never ? '\0' : '\'';

        var cfg = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = header,
            Quote = quote,
            Escape = quote,
            NewLine = crlf ? "\r\n" : "\n",
            BadDataFound = args =>
            {
                Assert.Fail($"Bad data found. Field: '{args.Field}', RawRecord: '{args.RawRecord}'");
            },
        };

        using var reader = new CsvHelper.CsvReader(source, cfg);

        List<Obj> actual = new(1024);
        actual.AddRange(reader.GetRecords<Obj>());
        actual.Sort();

        Assert.Equal(TestDataGenerator.Objects.Value, actual);
    }

    private static StringReader FromStringBuilder(StringBuilder sb)
    {
        var chunks = sb.GetChunks();
        Assert.True(chunks.MoveNext());
        var reader = new StringReader(StringPool.Shared.GetOrAdd(chunks.Current.Span));
        Assert.False(chunks.MoveNext());
        return reader;
    }

    private static StreamReader FromArrayPoolBufferWriter(ArrayPoolBufferWriter<byte> bufferWriter)
    {
        ArraySegment<byte> written = bufferWriter.DangerousGetArray();
        MemoryStream ms = new(written.Array!, written.Offset, written.Count, writable: false, publiclyVisible: false);
        return new StreamReader(ms, Encoding.UTF8);
    }
}
