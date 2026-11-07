using System.Globalization;
using System.Text;
using FlameCsv.IO.Internal;
using FlameCsv.Tests.TestData;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public static class WriterEdgeCaseTests
{
    [Flags]
    public enum EscapeFlags
    {
        None = 0,
        A = 1,
        B = 2,
    }

    private sealed class Quoted;

    private sealed class QuotedConverter<T> : CsvConverter<T, Quoted>
        where T : unmanaged, IBinaryInteger<T>
    {
        public override bool TryParse(ReadOnlySpan<T> source, out Quoted value) => throw new NotSupportedException();

        public override bool TryFormat(Span<T> destination, Quoted value, out int charsWritten)
        {
            ReadOnlySpan<char> text = "x,\"y\"";

            if (destination.Length < text.Length)
            {
                charsWritten = 0;
                return false;
            }

            for (int i = 0; i < text.Length; i++)
                destination[i] = T.CreateTruncating(text[i]);

            charsWritten = text.Length;
            return true;
        }
    }

    [Fact]
    public static void Should_Escape_Non_Primitive_Values_With_Default_Options()
    {
        // default options have a fast path that skips escaping for primitives; other values must still be escaped
        foreach (bool utf8 in (bool[])[false, true])
        {
            string result;

            if (utf8)
            {
                var ms = new MemoryStream();
                using (var writer = new CsvFieldWriter<byte>(new StreamBufferWriter(ms, default), CsvOptions<byte>.Default))
                {
                    writer.WriteField(CsvOptions<byte>.Default.GetConverter<EscapeFlags>(), EscapeFlags.A | EscapeFlags.B);
                    writer.WriteDelimiter();
                    writer.WriteField(new QuotedConverter<byte>(), new Quoted());
                    writer.WriteDelimiter();
                    writer.WriteField(CsvOptions<byte>.Default.GetConverter<int>(), -1);
                    writer.Writer.Drain();
                }
                result = Encoding.UTF8.GetString(ms.ToArray());
            }
            else
            {
                var sb = new StringBuilder();
                using (var writer = new CsvFieldWriter<char>(new TextBufferWriter(new StringWriter(sb), default), CsvOptions<char>.Default))
                {
                    writer.WriteField(CsvOptions<char>.Default.GetConverter<EscapeFlags>(), EscapeFlags.A | EscapeFlags.B);
                    writer.WriteDelimiter();
                    writer.WriteField(new QuotedConverter<char>(), new Quoted());
                    writer.WriteDelimiter();
                    writer.WriteField(CsvOptions<char>.Default.GetConverter<int>(), -1);
                    writer.Writer.Drain();
                }
                result = sb.ToString();
            }

            Assert.Equal("\"A, B\",\"x,\"\"y\"\"\",-1", result);
        }
    }

    [Fact]
    public static void Should_Handle_String_To_Bytes_With_Escapes()
    {
        var ms = new MemoryStream();

        using (var writer = new CsvFieldWriter<byte>(new StreamBufferWriter(ms, default), CsvOptions<byte>.Default))
        {
            writer.WriteText("test\"");
            writer.WriteDelimiter();
            writer.WriteText("\"test\"");
            writer.WriteDelimiter();
            writer.WriteText("\"test");
            writer.Writer.Drain();
        }

        Assert.Equal(
            """"
            "test""","""test""","""test"
            """",
            Encoding.UTF8.GetString(ms.ToArray())
        );
    }

    [Fact]
    public static void Should_Format_Doubles_Culture_Agnostic()
    {
        foreach (var culture in (string[])["en-US", "fi-FI", "fr-FR", "de-DE", "ru-RU", "ja-JP", "zh-CN", "tr-TR"])
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

            var sw = new StringWriter();
            using (var writer = Csv.To(sw).ToWriter())
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
        using (var writer = Csv.To(sw).ToWriter())
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
