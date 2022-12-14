using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Extensions;
using FlameCsv.Parsers.Text;
using FlameCsv.Parsers.Utf8;
using FlameCsv.Readers;
using FlameCsv.Tests.TestData;
using FlameCsv.Tests.Utilities;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public class CsvReaderTests : PooledBufferVerifier
{
    public enum CsvApi
    {
        Async,
        Sync,
        Enumerator,
    }

    public static IEnumerable<object[]> GetTestParameters()
    {
        return
            from api in new[] { CsvApi.Async, CsvApi.Sync, CsvApi.Enumerator }
            from type in new[] { typeof(char), typeof(byte) }
            from bufferSize in new[] { -1, 17, 128, 1024, 8096 }
            from crlf in new[] { true, false }
            from writeHeader in new[] { true, false }
            from writeTrailingNewline in new[] { true, false }
            from hasStrings in new[] { true, false }
            from hasWhitespace in new[] { true, false }
            select new object[]
                { api, type, bufferSize, crlf, writeHeader, writeTrailingNewline, hasStrings, hasWhitespace };
    }

    /// <summary>
    /// A spray-and-pray "integration" test of different APIs using various CSV data.
    /// </summary>
    [Theory, MemberData(nameof(GetTestParameters))]
    public async Task Should_Read(
        CsvApi api,
        Type type,
        int bufferSize,
        bool CRLF,
        bool header,
        bool trailingLF,
        bool strings,
        bool whitespace)
    {
        using var writer = new ArrayPoolBufferWriter<char>();

        string newLine = CRLF ? "\r\n" : "\n";
        List<Obj> items = new();

        if (type == typeof(char))
        {
            var options = CsvOptions.GetTextReaderDefault(new CsvTextParsersConfig { DateTimeFormat = "O" });
            options.Tokens = options.Tokens.WithNewLine(newLine);

            if (whitespace)
                options.tokens = options.tokens.WithWhitespace(" ");
            options.HasHeader = header;

            if (api == CsvApi.Async)
            {
                using var owner = GetDataBytes();
                var segment = owner.DangerousGetArray();
                using var reader = new StreamReader(
                    new MemoryStream(segment.Array!, segment.Offset, segment.Count),
                    Encoding.UTF8,
                    bufferSize: bufferSize);

                await foreach (var obj in CsvReader.ReadAsync<Obj>(reader, options))
                {
                    items.Add(obj);
                }
            }
            else
            {
                var sequence = MemorySegment<char>.AsSequence(GetDataChars(), bufferSize);

                if (api == CsvApi.Sync)
                {
                    foreach (var obj in CsvReader.Read<char, Obj>(sequence, options))
                    {
                        items.Add(obj);
                    }
                }
                else
                {
                    EnumerateToList(header, sequence, options, items);
                }
            }
        }
        else if (type == typeof(byte))
        {
            var options = CsvOptions.GetUtf8ReaderDefault(new CsvUtf8ParsersConfig { DateTimeFormat = 'O' });
            options.Tokens = options.Tokens.WithNewLine(newLine);

            if (whitespace)
                options.tokens = options.tokens.WithWhitespace(" ");
            options.HasHeader = header;

            using var owner = GetDataBytes();

            if (api == CsvApi.Async)
            {
                var segment = owner.DangerousGetArray();
                var pipeReader = PipeReader.Create(
                    new MemoryStream(segment.Array!, segment.Offset, segment.Count),
                    new StreamPipeReaderOptions(bufferSize: bufferSize));

                await foreach (var obj in CsvReader.ReadAsync<Obj>(pipeReader, options))
                {
                    items.Add(obj);
                }
            }
            else
            {
                var sequence = MemorySegment<byte>.AsSequence(owner.Memory, bufferSize);

                if (api == CsvApi.Sync)
                {
                    foreach (var obj in CsvReader.Read<byte, Obj>(sequence, options))
                    {
                        items.Add(obj);
                    }
                }
                else
                {
                    EnumerateToList(header, sequence, options, items);
                }
            }
        }
        else
        {
            Assert.True(false);
        }

        Assert.Equal(1_000, items.Count);

        for (int i = 0; i < 1_000; i++)
        {
            var obj = items[i];
            Assert.Equal(i, obj.Id);
            Assert.Equal(strings ? $"Name\"{i}" : $"Name-{i}", obj.Name);
            Assert.Equal(i % 2 == 0, obj.IsEnabled);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(i), obj.LastLogin);
            Assert.Equal(new Guid(i, 0, 0, TestDataGenerator._guidbytes), obj.Token);
        }

        MemoryOwner<byte> GetDataBytes()
        {
            TestDataGenerator.Generate(writer, newLine, header, trailingLF, strings, whitespace);
            var owner = MemoryOwner<byte>.Allocate(Encoding.UTF8.GetByteCount(writer.WrittenSpan));
            Assert.Equal(owner.Length, Encoding.UTF8.GetBytes(writer.WrittenSpan, owner.Span));
            return owner;
        }

        ReadOnlyMemory<char> GetDataChars()
        {
            TestDataGenerator.Generate(writer, newLine, header, trailingLF, strings, whitespace);
            return writer.WrittenMemory;
        }
    }

    [Fact]
    public async Task Should_Read_Long_Multisegment_Lines()
    {
        string name = new string('x', 1024);
        string data = $"0,{name},true,{DateTime.UnixEpoch:o},{Guid.Empty}{Environment.NewLine}";

        var objs = new List<Obj>();

        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(data));
        using var reader = new StreamReader(ms, bufferSize: 128);
        var options = CsvReaderOptions<char>.Default;

        await foreach (var item in CsvReader.ReadAsync<Obj>(reader, options))
        {
            objs.Add(item);
        }

        Assert.Single(objs);
        var obj = objs[0];
        Assert.Equal(0, obj.Id);
        Assert.Equal(name, obj.Name);
        Assert.True(obj.IsEnabled);
        Assert.Equal(DateTime.UnixEpoch, obj.LastLogin);
        Assert.Equal(Guid.Empty, obj.Token);
    }

    private static void EnumerateToList<T>(
        bool skipFirst,
        ReadOnlySequence<T> sequence,
        CsvReaderOptions<T> options,
        ICollection<Obj> items) where T : unmanaged, IEquatable<T>
    {
        int index = 1;
        long tokenPosition = 0;

        foreach (var record in CsvReader.Enumerate(sequence, options))
        {
            Assert.Equal(index++, record.Line);
            Assert.Equal(tokenPosition, record.Position);

            tokenPosition += record.Data.Length + options.tokens.NewLine.Length;

            if (skipFirst)
            {
                skipFirst = false;
                continue;
            }

            items.Add(
                new Obj
                {
                    Id = record.GetValue<int>(0),
                    Name = record.GetValue<string?>(1),
                    IsEnabled = record.GetValue<bool>(2),
                    LastLogin = record.GetValue<DateTimeOffset>(3),
                    Token = record.GetValue<Guid>(4),
                });
        }
    }
}
