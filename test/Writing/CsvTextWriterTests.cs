using System.Buffers;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
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
        bool? guarded
    )
    {
        using var pool = new ReturnTrackingBufferPool(guarded);
        var options = new CsvOptions<char>
        {
            Newline = newline,
            HasHeader = header,
            FieldQuoting = quoting,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
        };

        StringBuilder output = StringBuilderPool.Value.Get();

        if (sourceGen)
        {
            if (outputType)
            {
                CsvWriter.Write(
                    new StringWriter(output),
                    TestDataGenerator.Objects.Value,
                    ObjCharTypeMap.Default,
                    options,
                    new() { BufferSize = bufferSize, BufferPool = pool }
                );
            }
            else
            {
                CsvWriter.WriteToString(TestDataGenerator.Objects.Value, ObjCharTypeMap.Default, options, output);
            }
        }
        else
        {
            if (outputType)
            {
                CsvWriter.Write(
                    new StringWriter(output),
                    TestDataGenerator.Objects.Value,
                    options,
                    new() { BufferSize = bufferSize }
                );
            }
            else
            {
                CsvWriter.WriteToString(TestDataGenerator.Objects.Value, options, output);
            }
        }

        Validate(output, newline.IsCRLF(), header, quoting);
        StringBuilderPool.Value.Return(output);
    }

    [Theory, MemberData(nameof(Args))]
    public async Task Objects_Async(
        CsvNewline newline,
        bool header,
        CsvFieldQuoting quoting,
        bool sourceGen,
        int bufferSize,
        bool outputType,
        bool? guarded
    )
    {
        if (outputType)
            return;

        using var pool = new ReturnTrackingBufferPool(guarded);
        var options = new CsvOptions<char>
        {
            Newline = newline,
            HasHeader = header,
            FieldQuoting = quoting,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
        };

        StringBuilder output = StringBuilderPool.Value.Get();

        if (sourceGen)
        {
            await CsvWriter.WriteAsync(
                new StringWriter(output),
                TestDataGenerator.Objects.Value,
                ObjCharTypeMap.Default,
                options,
                new() { BufferSize = bufferSize, BufferPool = pool },
                cancellationToken: TestContext.Current.CancellationToken
            );
        }
        else
        {
            await CsvWriter.WriteAsync(
                new StringWriter(output),
                TestDataGenerator.Objects.Value,
                options,
                new() { BufferSize = bufferSize, BufferPool = pool },
                cancellationToken: TestContext.Current.CancellationToken
            );
        }

        Validate(output, newline.IsCRLF(), header, quoting);
        StringBuilderPool.Value.Return(output);
    }

    [Fact]
    public static async Task Should_Write_Async_Enumerable()
    {
        await using StringWriter sw = new();
        await CsvWriter.WriteAsync(
            sw,
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

    private static void Validate(StringBuilder sb, bool crlf, bool header, CsvFieldQuoting quoting)
    {
        ReadOnlySequence<char> sequence = StringBuilderSegment.Create(sb);
        Assert.True(sequence.IsSingleSegment);

        bool headerRead = false;
        int index = 0;

        const string date = "1970-01-01T00:00:00.0000000+00:00";
        const string dateQuoted = $"'{date}'";

        foreach (var current in sequence.First.Span.Tokenize('\n'))
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

            var line = current;

            if (crlf && !line.IsEmpty)
            {
                Assert.Equal('\r', line[^1]);
                line = line[..^1];
            }

            if (header && !headerRead)
            {
                Assert.Equal(0, index);
                Assert.Equal(
                    quoting == CsvFieldQuoting.Always ? TestDataGenerator.HeaderQuoted : TestDataGenerator.Header,
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

            foreach (var column in line.Tokenize(','))
            {
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
                        Assert.Equal(quoting == CsvFieldQuoting.Always ? dateQuoted : date, column);
                        break;
                    case 4:
                        var guid = new Guid(index, 0, 0, TestDataGenerator.GuidBytes);
                        Assert.Equal(quoting == CsvFieldQuoting.Always ? $"'{guid}'" : $"{guid}", column);
                        break;
                    default:
                        throw new XunitException($"Invalid column count on line {index}: {line}");
                }
            }

            index++;
        }

        Assert.Equal(1_000, index);
    }
}
