using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Enumerables;
using FlameCsv.Tests.TestData;
using FlameCsv.Writing;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Xunit.Sdk;

namespace FlameCsv.Tests.Writing;

public class CsvTextWriterTests : CsvWriterTestsBase
{
    [Theory, MemberData(nameof(Args))]
    public void Objects_Sync(
        string newline,
        bool header,
        char? escape,
        CsvFieldEscaping quoting,
        bool sourceGen)
    {
        var options = new CsvTextOptions
        {
            Newline = newline,
            HasHeader = header,
            FieldEscaping = quoting,
            Escape = escape,
            Quote = '\'',
            DateTimeFormat = "O",
        };

        var output = new StringBuilder(capacity: short.MaxValue * 4);

        if (sourceGen)
        {
            CsvWriter.Write(TestDataGenerator.Objects.Value, new StringWriter(output), ObjCharTypeMap.Instance, options);
        }
        else
        {
            CsvWriter.Write(TestDataGenerator.Objects.Value, new StringWriter(output), options);
        }

        Validate(output, escape.HasValue, newline == "\r\n", header, quoting);
    }

    [Theory, MemberData(nameof(Args))]
    public async Task Objects_Async(
        string newline,
        bool header,
        char? escape,
        CsvFieldEscaping quoting,
        bool sourceGen)
    {
        var options = new CsvTextOptions
        {
            Newline = newline,
            HasHeader = header,
            FieldEscaping = quoting,
            Escape = escape,
            Quote = '\'',
            DateTimeFormat = "O",
        };

        var output = new StringBuilder(capacity: short.MaxValue * 4);

        if (sourceGen)
        {
            await CsvWriter.WriteAsync(TestDataGenerator.Objects.Value, new StringWriter(output), ObjCharTypeMap.Instance, options);
        }
        else
        {
            await CsvWriter.WriteAsync(TestDataGenerator.Objects.Value, new StringWriter(output), options);
        }

        Validate(output, escape.HasValue, newline == "\r\n", header, quoting);
    }

    private static void Validate(
        StringBuilder sb,
        bool escapeMode,
        bool crlf,
        bool header,
        CsvFieldEscaping quoting)
    {
        var enumerator = sb.GetChunks();
        Assert.True(enumerator.MoveNext());
        ReadOnlySpan<char> result = enumerator.Current.Span;
        Assert.False(enumerator.MoveNext());

        bool headerRead = false;
        int index = 0;

        const string date = "1970-01-01T00:00:00.0000000+00:00";
        const string dateQuoted = "'1970-01-01T00:00:00.0000000+00:00'";

        foreach (var _line in new ReadOnlySpanTokenizer<char>(result, '\n'))
        {
            ReadOnlySpan<char> line = _line;

            if (crlf && !line.IsEmpty)
            {
                Assert.Equal('\r', line[^1]);
                line = line[..^1];
            }

            if (header && !headerRead)
            {
                Assert.Equal(0, index);
                Assert.Equal(TestDataGenerator.Header, line);
                headerRead = true;
                continue;
            }

            if (_line.IsEmpty)
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
                        Assert.Equal(quoting == CsvFieldEscaping.AlwaysQuote ? dateQuoted : date, column);
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
                        throw new XunitException($"Invalid column count on line {index}: {_line}");
                }
            }

            index++;
        }

        Assert.Equal(1_000, index);
    }
}

