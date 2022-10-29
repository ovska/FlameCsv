using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Writers;

namespace FlameCsv.Tests;

public class WriteTemp
{
    public sealed class CsvWriteState<T, T0, T1, T2, TResult>
    {
        private int written = 0;
        private Obj _value;
    }

    private sealed class HelloWorldFormatter : ICsvFormatter<char, string>
    {
        public const string HelloWorld = "Hello, World!";

        public bool TryFormat(string value, Span<char> buffer, out int tokensWritten)
        {
            if (buffer.Length >= HelloWorld.Length)
            {
                HelloWorld.CopyTo(buffer);
                tokensWritten = HelloWorld.Length;
                return true;
            }

            tokensWritten = 0;
            return false;
        }
    }

    [Fact]
    public static async Task Should_Write_Utf8()
    {
        await using var stream = new MemoryStream();

        var pipeWriter = PipeWriter.Create(stream);
        await using var writer = new CsvPipeWriter(pipeWriter);

        try
        {
            var formatter = new StringUtf8Formatter();

            for (int i = 0; i < 10_000; i++)
            {
                if (formatter.TryFormat("Hello, World!", writer.GetBuffer(), out int written))
                {
                    writer.Advance(written);
                    continue;
                }

                Memory<byte> buffer;

                do
                {
                    buffer = await writer.GrowAsync();
                } while (!formatter.TryFormat("Hello, World!", buffer.Span, out written));

                writer.Advance(written);
            }
        }
        catch (Exception e)
        {
            writer.Exception = e;
            throw;
        }

        await writer.FlushAsync(default);

        var str = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(HelloWorldFormatter.HelloWorld.Length * 10_000, str.Length);
    }

    [Fact(Skip = "TODO")]
    public static async Task Should_Write()
    {
        await using var stream = new MemoryStream();
        await using var textWriter = new StreamWriter(stream, Encoding.UTF8, bufferSize: 128);
        var writer = new CsvTextWriter(textWriter);

        HelloWorldFormatter formatter = new HelloWorldFormatter();

        for (int i = 0; i < 10_000; i++)
        {
            if (formatter.TryFormat("", writer.GetBuffer(), out int written))
            {
                writer.Advance(written);
                continue;
            }

            Memory<char> buffer;

            do
            {
                buffer = await writer.GrowAsync();
            } while (!formatter.TryFormat("", buffer.Span, out written));

            writer.Advance(written);
        }

        await writer.FlushAsync(default);
        await textWriter.FlushAsync();

        var str = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(HelloWorldFormatter.HelloWorld.Length * 10_000, str.Length);
    }

    private static bool TryWrite<TWriter>(ICsvFormatter<char, string> formatter, ref TWriter writer)
        where TWriter : ICsvWriter<char>
    {
        Span<char> span = writer.GetBuffer();

        if (formatter.TryFormat("", span, out var written))
        {
            writer.Advance(written);
            return true;
        }

        return false;
    }

    public class Obj
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Prop { get; init; }
    }

    public sealed class StringUtf8Formatter : ICsvFormatter<byte, string?>
    {
        public bool TryFormat(string? value, Span<byte> buffer, out int tokensWritten)
        {
            var span = value.AsSpan();
            if (Encoding.UTF8.GetMaxByteCount(span.Length) <= buffer.Length
                || Encoding.UTF8.GetByteCount(span) <= buffer.Length)
            {
                tokensWritten = Encoding.UTF8.GetBytes(span, buffer);
                return true;
            }

            Unsafe.SkipInit(out tokensWritten);
            return false;
        }
    }

    public sealed class StringFormatter : ICsvFormatter<char, string?>
    {
        public bool TryFormat(string? value, Span<char> buffer, out int tokensWritten)
        {
            if (value is null)
            {
                tokensWritten = 0;
                return true;
            }

            if (buffer.Length >= value.Length)
            {
                value.CopyTo(buffer);
                tokensWritten = value.Length;
                return true;
            }

            Unsafe.SkipInit(out tokensWritten);
            return false;
        }
    }
}
