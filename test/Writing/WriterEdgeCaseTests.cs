using System.Globalization;
using System.Text;
using FlameCsv.Tests.TestData;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public static class WriterEdgeCaseTests
{
    [Fact]
    public static void Should_Handle_String_To_Bytes_With_Escapes()
    {
        var ms = new MemoryStream();

        using (var writer = CsvFieldWriter.Create(ms, CsvOptions<byte>.Default))
        {
            writer.WriteText("test\"");
            writer.WriteDelimiter();
            writer.WriteText("\"test\"");
            writer.WriteDelimiter();
            writer.WriteText("\"test");
            writer.Writer.Flush();
        }

        Assert.Equal("\"test\"\"\",\"\"\"test\"\"\",\"\"\"test\"", Encoding.UTF8.GetString(ms.ToArray()));
    }

    [Fact]
    public static void Should_Format_Doubles_Culture_Agnostic()
    {
        foreach (var culture in (string[])["en-US", "fi-FI", "fr-FR", "de-DE", "ru-RU", "ja-JP", "zh-CN", "tr-TR"])
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

            var sw = new StringWriter();
            using (var writer = CsvWriter.Create(sw))
            {
                writer.WriteField(1234.56);
            }

            Assert.Equal("1234.56\r\n", sw.ToString());
        }
    }

    [Fact]
    public static void Should_Check_Escapes_Correctly()
    {
        var sw = new StringWriter();
        using (var writer = CsvWriter.Create(sw))
        {
            writer.WriteField("test\"\",");
        }

        Assert.Equal("\"test\"\"\"\",\"\r\n", sw.ToString());
    }

    [Fact]
    public static async Task Should_Not_Flush_On_Exception()
    {
        await using (StringWriter sw = new())
        {
            Assert.Throws<InvalidDataException>(() => Csv.To(sw).Write(InvalidData()));
            Assert.Empty(sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                Csv.To(sw).WriteAsync(InvalidData(), cancellationToken: TestContext.Current.CancellationToken)
            );
            Assert.Empty(sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                Csv.To(sw).WriteAsync(InvalidDataAsync(), cancellationToken: TestContext.Current.CancellationToken)
            );
            Assert.Empty(sw.ToString());
        }

        IEnumerable<Obj> InvalidData()
        {
            yield return new();
            throw new InvalidDataException();
        }

        async IAsyncEnumerable<Obj> InvalidDataAsync()
        {
            await Task.Yield();
            yield return new();
            throw new InvalidDataException();
        }
    }

    [Fact]
    public static async Task Should_Write_Header()
    {
        CsvOptions<char> options = CsvOptions<char>.Default;
        const string header = $"{TestDataGenerator.Header}\r\n";

        await using (StringWriter sw = new())
        {
            // ReSharper disable once MethodHasAsyncOverload
            Csv.To(sw).Write(Array.Empty<Obj>(), options);
            Assert.Equal(header, sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            // ReSharper disable once MethodHasAsyncOverload
            Csv.To(sw).Write(Empty(), options);
            Assert.Equal(header, sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Csv.To(sw).WriteAsync<Obj>([], options, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(header, sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Csv.To(sw).WriteAsync(Empty(), options, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(header, sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Csv.To(sw)
                .WriteAsync(
                    SyncAsyncEnumerable.Create(Empty()),
                    options,
                    cancellationToken: TestContext.Current.CancellationToken
                );
            Assert.Equal(header, sw.ToString());
        }
    }

    [Fact]
    public static async Task Should_Write_Empty_Line()
    {
        CsvOptions<char> options = new() { HasHeader = false };

        await using (StringWriter sw = new())
        {
            // ReSharper disable once MethodHasAsyncOverload
            Csv.To(sw).Write(Array.Empty<Obj>(), options);
            Assert.Equal("\r\n", sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            // ReSharper disable once MethodHasAsyncOverload
            Csv.To(sw).Write(Empty(), options);
            Assert.Equal("\r\n", sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Csv.To(sw).WriteAsync<Obj>([], options, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("\r\n", sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Csv.To(sw).WriteAsync(Empty(), options, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("\r\n", sw.ToString());
        }

        await using (StringWriter sw = new())
        {
            await Csv.To(sw)
                .WriteAsync(
                    SyncAsyncEnumerable.Create(Empty()),
                    options,
                    cancellationToken: TestContext.Current.CancellationToken
                );
            Assert.Equal("\r\n", sw.ToString());
        }
    }

    // force enumerator type, enumerable.empty returns a singleton
    private static IEnumerable<Obj> Empty()
    {
        yield break;
    }
}
