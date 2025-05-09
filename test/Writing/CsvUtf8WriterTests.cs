using System.IO.Pipelines;
using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Enumerables;
using FlameCsv.Extensions;
using FlameCsv.Tests.TestData;
using Xunit.Sdk;

namespace FlameCsv.Tests.Writing;

public class CsvUtf8WriterTests : CsvWriterTestsBase
{
    [Theory, MemberData(nameof(Args))]
    public void Objects_Sync(
        CsvNewline newline,
        bool header,
        char? escape,
        CsvFieldQuoting quoting,
        bool sourceGen,
        int bufferSize,
        bool outputType,
        bool? guarded
    )
    {
        if (outputType)
        {
            return; // pipes dont support synchronous writing
        }

        using var pool = ReturnTrackingMemoryPool<byte>.Create(guarded);
        var options = new CsvOptions<byte>
        {
            Newline = newline,
            HasHeader = header,
            FieldQuoting = quoting,
            Escape = escape,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
            MemoryPool = pool,
        };

        using var output = new MemoryStream(capacity: short.MaxValue * 4);

        if (sourceGen)
        {
            CsvWriter.Write(
                output,
                TestDataGenerator.Objects.Value,
                ObjByteTypeMap.Default,
                options,
                new() { BufferSize = bufferSize }
            );
        }
        else
        {
            CsvWriter.Write(output, TestDataGenerator.Objects.Value, options, new() { BufferSize = bufferSize });
        }

        Assert.True(output.TryGetBuffer(out var buffer));

        Validate(buffer, escape.HasValue, newline.IsCRLF(), header, quoting);
    }

    [Theory, MemberData(nameof(Args))]
    public async Task Objects_Async(
        CsvNewline newline,
        bool header,
        char? escape,
        CsvFieldQuoting quoting,
        bool sourceGen,
        int bufferSize,
        bool outputType,
        bool? guarded
    )
    {
        using var pool = ReturnTrackingMemoryPool<byte>.Create(guarded);
        var options = new CsvOptions<byte>
        {
            Newline = newline,
            HasHeader = header,
            FieldQuoting = quoting,
            Escape = escape,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
            MemoryPool = pool,
        };

        using var output = new MemoryStream(capacity: short.MaxValue * 4);

        if (sourceGen)
        {
            if (outputType)
            {
                await CsvWriter.WriteAsync(
                    PipeWriter.Create(output),
                    TestDataGenerator.Objects.Value,
                    ObjByteTypeMap.Default,
                    options,
                    TestContext.Current.CancellationToken
                );
            }
            else
            {
                await CsvWriter.WriteAsync(
                    output,
                    TestDataGenerator.Objects.Value,
                    ObjByteTypeMap.Default,
                    options,
                    new() { BufferSize = bufferSize },
                    cancellationToken: TestContext.Current.CancellationToken
                );
            }
        }
        else
        {
            if (outputType)
            {
                await CsvWriter.WriteAsync(
                    PipeWriter.Create(output),
                    TestDataGenerator.Objects.Value,
                    options,
                    TestContext.Current.CancellationToken
                );
            }
            else
            {
                await CsvWriter.WriteAsync(
                    output,
                    TestDataGenerator.Objects.Value,
                    options,
                    new() { BufferSize = bufferSize },
                    cancellationToken: TestContext.Current.CancellationToken
                );
            }
        }

        Assert.True(output.TryGetBuffer(out var buffer));

        Validate(buffer, escape.HasValue, newline.IsCRLF(), header, quoting);
    }

    private static void Validate(
        ReadOnlyMemory<byte> result,
        bool escapeMode,
        bool crlf,
        bool header,
        CsvFieldQuoting quoting
    )
    {
        bool headerRead = false;
        int index = 0;

        ReadOnlySpan<byte> date = "1970-01-01T00:00:00.0000000+00:00"u8;
        ReadOnlySpan<byte> dateQuoted = "'1970-01-01T00:00:00.0000000+00:00'"u8;

        var tokenizer = new ReadOnlySpanTokenizer<byte>(result.Span, (byte)'\n');

        while (tokenizer.MoveNext())
        {
            ReadOnlySpan<byte> line = tokenizer.Current;

            if (crlf && !line.IsEmpty)
            {
                Assert.Equal((byte)'\r', line[^1]);
                line = line[..^1];
            }

            if (header && !headerRead)
            {
                Assert.Equal(0, index);
                Assert.Equal(
                    quoting == CsvFieldQuoting.Always ? TestDataGenerator.HeaderQuotedU8 : TestDataGenerator.HeaderU8,
                    line
                );
                headerRead = true;
                continue;
            }

            if (line.IsEmpty)
            {
                Assert.Equal(1000, index);
                continue;
            }

            int columnIndex = 0;

            foreach (var columnBytes in line.Tokenize((byte)','))
            {
                string column = Encoding.UTF8.GetString(columnBytes);

                switch (columnIndex++)
                {
                    case 0:
                        Assert.Equal(quoting == CsvFieldQuoting.Always ? $"'{index}'" : $"{index}", column);
                        break;
                    case 1:
                        if (quoting == CsvFieldQuoting.Never)
                        {
                            Assert.Equal($" Name'{index}", column);
                        }
                        else if (escapeMode)
                        {
                            Assert.Equal($"' Name^'{index}'", column);
                        }
                        else
                        {
                            Assert.Equal($"' Name''{index}'", column);
                        }

                        break;
                    case 2:
                        string val = index % 2 == 0 ? "true" : "false";
                        Assert.Equal(quoting == CsvFieldQuoting.Always ? $"'{val}'" : $"{val}", column);
                        break;
                    case 3:
                        Assert.Equal(quoting == CsvFieldQuoting.Always ? dateQuoted : date, columnBytes);
                        break;
                    case 4:
                        var guid = new Guid(index, 0, 0, TestDataGenerator.GuidBytes);
                        Assert.Equal(quoting == CsvFieldQuoting.Always ? $"'{guid}'" : $"{guid}", column);
                        break;
                    default:
                        throw new XunitException(
                            $"Invalid column count on line {index}: {Encoding.UTF8.GetString(line)}"
                        );
                }
            }

            index++;
        }

        Assert.Equal(1_000, index);
    }
}
