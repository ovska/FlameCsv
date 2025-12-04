using System.Globalization;
using System.IO.Pipelines;
using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;
using FlameCsv.Tests.TestData;

namespace FlameCsv.Tests.Writing;

public class CsvUtf8WriterTests : CsvWriterTestsBase
{
    public static IEnumerable<TheoryDataRow<CsvNewline, bool, CsvFieldQuoting, bool, int, bool, bool, bool?>> SyncArgs
    {
        get => Args().Where(data => !data.Data.Item6); // can't sync write into pipewriter
    }

    [Theory, MemberData(nameof(SyncArgs))]
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
        Assert.False(outputType, "Synchronous writing to PipeWriter is not supported.");

        using var pool = new ReturnTrackingBufferPool(guarded);
        var options = new CsvOptions<byte>
        {
            Newline = newline,
            HasHeader = header,
            FieldQuoting = quoting,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
        };

        using var writer = new ArrayPoolBufferWriter<byte>(initialCapacity: short.MaxValue * 4);
        using var output = writer.AsStream();

        var builder = Csv.To(output, new() { BufferSize = bufferSize, BufferPool = pool });

        if (sourceGen)
        {
            if (parallel)
            {
                builder
                    .AsParallel(TestContext.Current.CancellationToken)
                    .WriteUnordered(ObjByteTypeMap.Default, TestDataGenerator.Objects.Value, options);
            }
            else
            {
                builder.Write(ObjByteTypeMap.Default, TestDataGenerator.Objects.Value, options);
            }
        }
        else
        {
            if (parallel)
            {
                builder
                    .AsParallel(TestContext.Current.CancellationToken)
                    .WriteUnordered(TestDataGenerator.Objects.Value, options);
            }
            else
            {
                builder.Write(TestDataGenerator.Objects.Value, options);
            }
        }

        Validate(writer, newline.IsCRLF(), header, quoting);
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
        var options = new CsvOptions<byte>
        {
            Newline = newline,
            HasHeader = header,
            FieldQuoting = quoting,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
        };

        using var writer = new ArrayPoolBufferWriter<byte>(initialCapacity: short.MaxValue * 4);
        using var output = writer.AsStream();

        Csv.IWriteBuilder<byte> builder = outputType
            ? Csv.To(PipeWriter.Create(output, new(pool: pool._bytePool, minimumBufferSize: bufferSize)), pool)
            : Csv.To(output, new() { BufferSize = bufferSize, BufferPool = pool });

        if (sourceGen)
        {
            if (parallel)
            {
                await builder
                    .AsParallel(TestContext.Current.CancellationToken)
                    .WriteUnorderedAsync(ObjByteTypeMap.Default, TestDataGenerator.Objects.Value, options);
            }
            else
            {
                await builder.WriteAsync(
                    ObjByteTypeMap.Default,
                    TestDataGenerator.Objects.Value,
                    options,
                    TestContext.Current.CancellationToken
                );
            }
        }
        else
        {
            if (parallel)
            {
                await builder
                    .AsParallel(TestContext.Current.CancellationToken)
                    .WriteUnorderedAsync(TestDataGenerator.Objects.Value, options);
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

        Validate(writer, newline.IsCRLF(), header, quoting);
    }

    private static void Validate(ArrayPoolBufferWriter<byte> result, bool crlf, bool header, CsvFieldQuoting quoting)
    {
        ArraySegment<byte> written = result.DangerousGetArray();
        var ms = new MemoryStream(written.Array!, written.Offset, written.Count, writable: false);

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

        using var reader = new CsvHelper.CsvReader(new StreamReader(ms, Encoding.UTF8), cfg);

        List<Obj> actual = new(1024);
        actual.AddRange(reader.GetRecords<Obj>());
        actual.Sort();

        Assert.Equal(TestDataGenerator.Objects.Value, actual);
    }
}
