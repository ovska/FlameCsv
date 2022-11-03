using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Binding.Providers;
using FlameCsv.Extensions;
using FlameCsv.Parsers.Text;
using FlameCsv.Parsers.Utf8;
using FlameCsv.Readers;
using FlameCsv.Tests.TestData;
using FlameCsv.Tests.Utilities;

// ReSharper disable ConvertIfStatementToSwitchStatement

// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public static class CsvReaderTests
{
    public enum CsvApi
    {
        Async,
        Sync,
        Enumerator
    }

    public static IEnumerable<object[]> GetTestParameters()
    {
        return
            from api in new[] { CsvApi.Async, CsvApi.Sync, CsvApi.Enumerator }
            from type in new[] { typeof(char), typeof(byte) }
            from bufferSize in new[] { -1, 17, 128, 1024, 8096 }
            from newline in new[] { "\n", "\r\n" }
            from writeHeader in new[] { true, false }
            from writeTrailingNewline in new[] { true, false }
            select new object[] { api, type, bufferSize, newline, writeHeader, writeTrailingNewline };
    }

    [Theory]
    [MemberData(nameof(GetTestParameters))]
    public static async Task Should_Read(
        CsvApi api,
        Type type,
        int bufferSize,
        string newLine,
        bool writeHeader,
        bool writeTrailingNewline)
    {
        List<Obj> items = new();

        if (type == typeof(char))
        {
            var options = CsvOptions.GetTextReaderDefault(new CsvTextParsersConfig { DateTimeFormat = "O" });
            options.Tokens = options.Tokens.WithNewLine(newLine);
            if (!writeHeader)
                options.SetBinder(new IndexBindingProvider<char>());
            else
                options.SetBinder(new HeaderTextBindingProvider<Obj>());

            if (api == CsvApi.Async)
            {
                using var reader = new StreamReader(
                    new MemoryStream(GetDataBytes()),
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
                    EnumerateToList(writeHeader, sequence, options, items);
                }
            }
        }
        else if (type == typeof(byte))
        {
            var options = CsvOptions.GetUtf8ReaderDefault(new CsvUtf8ParsersConfig { DateTimeFormat = 'O' });
            options.Tokens = options.Tokens.WithNewLine(newLine);
            if (!writeHeader)
                options.SetBinder(new IndexBindingProvider<byte>());
            else
                options.SetBinder(new HeaderUtf8BindingProvider<Obj>());

            if (api == CsvApi.Async)
            {
                var pipeReader = PipeReader.Create(
                    new MemoryStream(GetDataBytes()),
                    new StreamPipeReaderOptions(bufferSize: bufferSize));

                await foreach (var obj in CsvReader.ReadAsync<Obj>(pipeReader, options))
                {
                    items.Add(obj);
                }
            }
            else
            {
                var sequence = MemorySegment<byte>.AsSequence(GetDataBytes(), bufferSize);

                if (api == CsvApi.Sync)
                {
                    foreach (var obj in CsvReader.Read<byte, Obj>(sequence, options))
                    {
                        items.Add(obj);
                    }
                }
                else
                {
                    EnumerateToList(writeHeader, sequence, options, items);
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
            Assert.Equal($"Name-{i}", obj.Name);
            Assert.Equal(i % 2 == 0, obj.IsEnabled);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(i), obj.LastLogin);
            Assert.Equal(new Guid(i, 0, 0, TestDataGenerator._guidbytes), obj.Token);
        }

        byte[] GetDataBytes()
        {
            using var writer = new ArrayPoolBufferWriter<char>();
            TestDataGenerator.Generate(writer, newLine, writeHeader, writeTrailingNewline);
            var data = new byte[Encoding.UTF8.GetByteCount(writer.WrittenSpan)];
            Assert.Equal(data.Length, Encoding.UTF8.GetBytes(writer.WrittenSpan, data));
            return data;
        }

        char[] GetDataChars()
        {
            using var writer = new ArrayPoolBufferWriter<char>();
            TestDataGenerator.Generate(writer, newLine, writeHeader, writeTrailingNewline);
            return writer.WrittenSpan.ToArray();
        }
    }

    [Fact]
    public static async Task Should_Read_Long_Multisegment_Lines()
    {
        string name = new string('x', 1024);
        string data = $"0,{name},true,{DateTime.UnixEpoch:o},{Guid.Empty}{Environment.NewLine}";

        var objs = new List<Obj>();

        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes(data));
        using var reader = new StreamReader(ms, bufferSize: 128);
        var options = CsvReaderOptions<char>.Default.SetBinder(new IndexBindingProvider<char>());

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
        foreach (var record in CsvReader.Enumerate(sequence, options))
        {
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
