using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Formatters;
using FlameCsv.Writing;

namespace FlameCsv.Tests;

public static class WriteTemp
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

    public class Obj
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Prop { get; init; }
    }

    [Fact]
    public static async Task SHOULD_WRITE_TEST()
    {
        var formatter = new StringFormatter();
        var stringWriter = new StringWriter();
        var textPipe = new CsvCharBufferWriter(stringWriter, AllocatingArrayPool<char>.Instance);

        var opts = new CsvWriterOptions<char> { ArrayPool = AllocatingArrayPool<char>.Instance };
        await using (var writer = new CsvRecordWriter<char, CsvCharBufferWriter>(textPipe, opts))
        {
            try
            {
                for (var i = 0; i < 1000; i++)
                {
                    writer.WriteValue(formatter, i.ToString());

                    if (i < 999)
                        writer.WriteDelimiter();
                }
            }
            catch (Exception e)
            {
                writer.Exception = e;
                throw;
            }
        }

        var result = stringWriter.ToString();
        Assert.Equal(string.Join(',', Enumerable.Range(0, 1000)), result);
    }

    private static bool TryWrite<TWriter>(ICsvFormatter<char, string> formatter, ref TWriter writer)
        where TWriter : IAsyncBufferWriter<char>
    {
        Span<char> span = writer.GetMemory().Span;

        if (formatter.TryFormat("", span, out var written))
        {
            writer.Advance(written);
            return true;
        }

        return false;
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
