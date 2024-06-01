using System.Buffers;
using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Enumerables;
using FlameCsv.Tests.TestData;
using FlameCsv.Writing;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Xunit.Sdk;

namespace FlameCsv.Tests.Writing;

public class CsvUtf8WriterTests : CsvWriterTestsBase
{
    [Theory, MemberData(nameof(ArgsWithBufferSize))]
    public async Task Objects_Async(
        string newline,
        bool header,
        char? escape,
        CsvFieldEscaping quoting,
        bool sourceGen,
        int bufferSize)
    {
        var options = new CsvUtf8Options
        {
            Newline = newline,
            HasHeader = header,
            FieldEscaping = quoting,
            Escape = escape,
            Quote = '\'',
            DateTimeFormat = 'O',
        };

        using var output = new MemoryStream(capacity: short.MaxValue * 4);

        if (sourceGen)
        {
            await CsvWriter.WriteAsync(TestDataGenerator.Objects.Value, output, ObjByteTypeMap.Instance, options, bufferSize: bufferSize);
        }
        else
        {
            await CsvWriter.WriteAsync(TestDataGenerator.Objects.Value, output, options, bufferSize: bufferSize);
        }

        Assert.True(output.TryGetBuffer(out var buffer));

        Validate(buffer, escape.HasValue, newline == "\r\n", header, quoting);
    }

    private static void Validate(
        ReadOnlyMemory<byte> result,
        bool escapeMode,
        bool crlf,
        bool header,
        CsvFieldEscaping quoting)
    {
        bool headerRead = false;
        int index = 0;

        ReadOnlySpan<byte> date = "1970-01-01T00:00:00.0000000+00:00"u8;
        ReadOnlySpan<byte> dateQuoted = "'1970-01-01T00:00:00.0000000+00:00'"u8;

        foreach (var _line in new ReadOnlySpanTokenizer<byte>(result.Span, (byte)'\n'))
        {
            ReadOnlySpan<byte> line = _line;

            if (crlf && !line.IsEmpty)
            {
                Assert.Equal((byte)'\r', line[^1]);
                line = line[..^1];
            }

            if (header && !headerRead)
            {
                Assert.Equal(0, index);
                Assert.Equal(
                    quoting == CsvFieldEscaping.AlwaysQuote ? TestDataGenerator.HeaderQuotedU8 : TestDataGenerator.HeaderU8,
                    line);
                headerRead = true;
                continue;
            }

            if (_line.IsEmpty)
            {
                Assert.Equal(1000, index);
                continue;
            }

            int columnIndex = 0;

            foreach (var _column in line.Tokenize((byte)','))
            {
                string column = Encoding.UTF8.GetString(_column);

                switch (columnIndex++)
                {
                    case 0:
                        if (quoting == CsvFieldEscaping.AlwaysQuote)
                        {
                            Assert.Equal($"'{index}'", column);
                        }
                        else
                        {
                            Assert.Equal($"{index}", column);
                        }
                        break;
                    case 1:
                        if (quoting == CsvFieldEscaping.Never)
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
                        if (quoting == CsvFieldEscaping.AlwaysQuote)
                        {
                            Assert.Equal($"'{val}'", column);
                        }
                        else
                        {
                            Assert.Equal($"{val}", column);
                        }
                        break;
                    case 3:
                        Assert.Equal(quoting == CsvFieldEscaping.AlwaysQuote ? dateQuoted : date, _column);
                        break;
                    case 4:
                        var guid = new Guid(index, 0, 0, TestDataGenerator._guidbytes);
                        if (quoting == CsvFieldEscaping.AlwaysQuote)
                        {
                            Assert.Equal($"'{guid}'", column);
                        }
                        else
                        {
                            Assert.Equal($"{guid}", column);
                        }
                        break;
                    default:
                        throw new XunitException($"Invalid column count on line {index}: {Encoding.UTF8.GetString(_line)}");
                }
            }

            index++;
        }

        Assert.Equal(1_000, index);
    }
}

