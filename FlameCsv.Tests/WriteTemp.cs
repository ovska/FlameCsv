using System.Diagnostics.CodeAnalysis;
using FlameCsv.Converters;
using FlameCsv.Extensions;
using FlameCsv.Writing;

namespace FlameCsv.Tests;

public static class WriteTemp
{
    private sealed class HelloWorldFormatter : CsvConverter<char, string>
    {
        public const string HelloWorld = "Hello, World!";

        public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
        {
            throw new NotImplementedException();
        }

        public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
        {
            if (destination.Length >= HelloWorld.Length)
            {
                HelloWorld.CopyTo(destination);
                charsWritten = HelloWorld.Length;
                return true;
            }

            charsWritten = 0;
            return false;
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
        var formatter = new StringTextConverter();
        var stringWriter = new StringWriter();
        var textPipe = new CsvCharBufferWriter(stringWriter, AllocatingArrayPool<char>.Instance);

        var opts = new CsvTextOptions { ArrayPool = AllocatingArrayPool<char>.Instance };
        await using (var writer = new CsvRecordWriter<char, CsvCharBufferWriter>(textPipe, opts))
        {
            try
            {
                for (var i = 0; i < 1000; i++)
                {
                    writer.WriteField(formatter, i.ToString());

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
}
