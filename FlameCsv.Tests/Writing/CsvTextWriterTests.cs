using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Enumerables;
using FlameCsv.Tests.TestData;
using FlameCsv.Tests.Utilities;
using FlameCsv.Writing;
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
        bool sourceGen,
        int bufferSize,
        bool? guarded)
    {
        using var pool = ReturnTrackingMemoryPool<char>.Create(guarded);
        var options = new CsvOptions<char>
        {
            Newline = newline,
            HasHeader = header,
            FieldEscaping = quoting,
            Escape = escape,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
            MemoryPool = pool,
        };

        var output = new StringBuilder(capacity: short.MaxValue * 4);

        if (sourceGen)
        {
            CsvWriter.Write(
                TestDataGenerator.Objects.Value,
                new StringWriter(output),
                ObjCharTypeMap.Instance,
                options,
                bufferSize: bufferSize);
        }
        else
        {
            CsvWriter.Write(TestDataGenerator.Objects.Value, new StringWriter(output), options, bufferSize: bufferSize);
        }

        Validate(output, escape.HasValue, newline == "\r\n", header, quoting);
    }

    [Theory, MemberData(nameof(Args))]
    public async Task Objects_Async(
        string newline,
        bool header,
        char? escape,
        CsvFieldEscaping quoting,
        bool sourceGen,
        int bufferSize,
        bool? guarded)
    {
        using var pool = ReturnTrackingMemoryPool<char>.Create(guarded);
        var options = new CsvOptions<char>
        {
            Newline = newline,
            HasHeader = header,
            FieldEscaping = quoting,
            Escape = escape,
            Quote = '\'',
            Formats = { { typeof(DateTime), "O" }, { typeof(DateTimeOffset), "O" } },
            MemoryPool = pool,
        };

        var output = new StringBuilder(capacity: short.MaxValue * 4);

        if (sourceGen)
        {
            await CsvWriter.WriteAsync(
                TestDataGenerator.Objects.Value,
                new StringWriter(output),
                ObjCharTypeMap.Instance,
                options,
                bufferSize: bufferSize);
        }
        else
        {
            await CsvWriter.WriteAsync(
                TestDataGenerator.Objects.Value,
                new StringWriter(output),
                options,
                bufferSize: bufferSize);
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

        var tokenizer = new ReadOnlySpanTokenizer<char>(result, '\n');

        while (tokenizer.MoveNext())
        {
            ReadOnlySpan<char> line = tokenizer.Current;

            if (crlf && !line.IsEmpty)
            {
                Assert.Equal('\r', line[^1]);
                line = line[..^1];
            }

            if (header && !headerRead)
            {
                Assert.Equal(0, index);
                Assert.Equal(
                    quoting == CsvFieldEscaping.AlwaysQuote ? TestDataGenerator.HeaderQuoted : TestDataGenerator.Header,
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

            foreach (var column in line.Tokenize(','))
            {
                switch (columnIndex++)
                {
                    case 0:
                        Assert.Equal(quoting == CsvFieldEscaping.AlwaysQuote ? $"'{index}'" : $"{index}", column);
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
                        Assert.Equal(quoting == CsvFieldEscaping.AlwaysQuote ? $"'{val}'" : $"{val}", column);
                        break;
                    case 3:
                        Assert.Equal(quoting == CsvFieldEscaping.AlwaysQuote ? dateQuoted : date, column);
                        break;
                    case 4:
                        var guid = new Guid(index, 0, 0, TestDataGenerator.GuidBytes);
                        Assert.Equal(quoting == CsvFieldEscaping.AlwaysQuote ? $"'{guid}'" : $"{guid}", column);
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
