using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Formatters;
using FlameCsv.Writers;

namespace FlameCsv.Tests;

public class WriteTemp
{
    private sealed class HelloWorldFormatter : ICsvFormatter<char, string>
    {
        public const string HelloWorld = "Hello, World!";

        public bool TryFormat(string value, Span<char> destination, out int tokensWritten)
        {
            if (destination.Length >= HelloWorld.Length)
            {
                HelloWorld.CopyTo(destination);
                tokensWritten = HelloWorld.Length;
                return true;
            }

            tokensWritten = 0;
            return false;
        }

        public bool CanFormat(Type resultType)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public static async Task Should_Write_Utf8()
    {
        await using var stream = new MemoryStream();

        var pipeWriter = PipeWriter.Create(stream);

        await using (var writer = new CsvPipeWriter(pipeWriter))
        {
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
        }


        var str = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal(HelloWorldFormatter.HelloWorld.Length * 10_000, str.Length);
    }

    internal enum FormatResult
    {
        Fail = 0,
        Success = 1,
        NeedEscape = 2,
    }

    private static bool TryFormat<T, TValue>(
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        Span<T> buffer,
        in CsvTokens<T> tokens,
        ref T[]? leftoverBuffer,
        out int tokensWritten) where T : unmanaged, IEquatable<T>
    {
        if (formatter.TryFormat(value, buffer, out tokensWritten))
        {
            var written = buffer[..tokensWritten];

            // if (WriteUtil.NeedsEscaping(written, tokens.StringDelimiter, out int quoteCount, out int escLen) ||
            //     (!tokens.Whitespace.IsEmpty && WriteUtil.NeedsEscaping(written, tokens.Whitespace, out escLen)))
            // {
            //     if (buffer.Length >= escLen)
            //     {
            //         WriteUtil.Escape(written, buffer, tokens.StringDelimiter, quoteCount);
            //         tokensWritten = escLen;
            //         return true;
            //     }
            //
            //     // Value was written but it needs to be escaped
            //     tokensWritten = default;
            //     return false;
            // }

            return true;
        }

        return false;
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
        public bool TryFormat(string? value, Span<byte> destination, out int tokensWritten)
        {
            var span = value.AsSpan();
            if (Encoding.UTF8.GetMaxByteCount(span.Length) <= destination.Length
                || Encoding.UTF8.GetByteCount(span) <= destination.Length)
            {
                tokensWritten = Encoding.UTF8.GetBytes(span, destination);
                return true;
            }

            Unsafe.SkipInit(out tokensWritten);
            return false;
        }

        public bool CanFormat(Type resultType)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class StringFormatter : ICsvFormatter<char, string?>
    {
        public bool TryFormat(string? value, Span<char> destination, out int tokensWritten)
        {
            if (value is null)
            {
                tokensWritten = 0;
                return true;
            }

            if (destination.Length >= value.Length)
            {
                value.CopyTo(destination);
                tokensWritten = value.Length;
                return true;
            }

            Unsafe.SkipInit(out tokensWritten);
            return false;
        }

        public bool CanFormat(Type resultType)
        {
            throw new NotImplementedException();
        }
    }
}
