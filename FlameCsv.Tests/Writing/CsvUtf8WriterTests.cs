using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Enumerables;
using FlameCsv.Tests.TestData;
using FlameCsv.Tests.Utilities;
using FlameCsv.Writing;
using Xunit.Sdk;

namespace FlameCsv.Tests.Writing;

public class CsvUtf8WriterTests : CsvWriterTestsBase
{
    [Theory, MemberData(nameof(Args))]
    public async Task Objects_Async(
        string newline,
        bool header,
        char? escape,
        CsvFieldQuoting quoting,
        bool sourceGen,
        int bufferSize,
        bool? guarded)
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
            await CsvWriter.WriteAsync(
                TestDataGenerator.Objects.Value,
                output,
                ObjByteTypeMap.Instance,
                options,
                bufferSize: bufferSize);
        }
        else
        {
            await CsvWriter.WriteAsync(
                TestDataGenerator.Objects.Value,
                output,
                options,
                bufferSize: bufferSize);
        }

        Assert.True(output.TryGetBuffer(out var buffer));

        Validate(buffer, escape.HasValue, newline == "\r\n", header, quoting);
    }

    private static void Validate(
        ReadOnlyMemory<byte> result,
        bool escapeMode,
        bool crlf,
        bool header,
        CsvFieldQuoting quoting)
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
                    quoting == CsvFieldQuoting.AlwaysQuote
                        ? TestDataGenerator.HeaderQuotedU8
                        : TestDataGenerator.HeaderU8,
                    line);
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
                        Assert.Equal(quoting == CsvFieldQuoting.AlwaysQuote ? $"'{index}'" : $"{index}", column);
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
                        Assert.Equal(quoting == CsvFieldQuoting.AlwaysQuote ? $"'{val}'" : $"{val}", column);
                        break;
                    case 3:
                        Assert.Equal(quoting == CsvFieldQuoting.AlwaysQuote ? dateQuoted : date, columnBytes);
                        break;
                    case 4:
                        var guid = new Guid(index, 0, 0, TestDataGenerator.GuidBytes);
                        Assert.Equal(quoting == CsvFieldQuoting.AlwaysQuote ? $"'{guid}'" : $"{guid}", column);
                        break;
                    default:
                        throw new XunitException(
                            $"Invalid column count on line {index}: {Encoding.UTF8.GetString(line)}");
                }
            }

            index++;
        }

        Assert.Equal(1_000, index);
    }
}
