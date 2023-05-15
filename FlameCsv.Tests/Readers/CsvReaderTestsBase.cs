using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Enumeration;
using FlameCsv.Tests.TestData;
using FlameCsv.Tests.Utilities;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

/// <summary>
/// A spray-and-pray tests of different APIs using various options and CSV features.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1158:Static member in generic type should use a type parameter.", Justification = "<Pending>")]
public abstract class CsvReaderTestsBase<T> : IDisposable
    where T : unmanaged, IEquatable<T>
{
    private static readonly int[] _bufferSizes = { -1, 17, 128, 1024, 8096 };
    private static readonly int[] _emptySegmentsEvery = { 0, 1, 7 };
    private static readonly string[] _crlf = { "CRLF", "LF" };
    private static readonly bool[] _booleans = { true, false };
    private static readonly Mode[] _escaping = { Mode.None, Mode.RFC, Mode.Escape };

    private ReturnTrackingArrayPool<T>? _pool;

    protected abstract CsvOptions<T> CreateOptions(string newline, char? escape);
    protected abstract IDisposable? GetMemory(ArrayPoolBufferWriter<char> writer, out ReadOnlyMemory<T> memory);
    protected abstract CsvRecordAsyncEnumerable<T> GetRecords(
        Stream stream,
        CsvOptions<T> options,
        int bufferSize);
    protected abstract IAsyncEnumerable<Obj> GetObjects(
        Stream stream,
        CsvOptions<T> options,
        int bufferSize);

    public static IEnumerable<object[]> SyncTypeParams
        =>
            from crlf in _crlf
            from writeHeader in _booleans
            from writeTrailingNewline in _booleans
            from bufferSize in _bufferSizes
            from emptySegmentFrequency in _emptySegmentsEvery
            from escaping in _escaping
            select new object[] { crlf, writeHeader, writeTrailingNewline, bufferSize, emptySegmentFrequency, escaping };

    public static IEnumerable<object[]> AsyncTypeParams
        =>
            from crlf in _crlf
            from writeHeader in _booleans
            from writeTrailingNewline in _booleans
            from bufferSize in _bufferSizes
            from escaping in _escaping
            select new object[] { crlf, writeHeader, writeTrailingNewline, bufferSize, escaping };

    [Theory, MemberData(nameof(SyncTypeParams))]
    public void Objects_Sync(
        string newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        int emptySegmentFreq,
        Mode escaping)
    {
        newline = newline == "LF" ? "\n" : "\r\n";
        CsvOptions<T> options = PrepareOptions(newline, header, escaping);

        List<Obj> items = new(1000);

        using (var writer = CsvReaderTestsBase<T>.GetWriter(newline, header, trailingLF, escaping))
        using (GetMemory(writer, out var memory))
        {
            var sequence = MemorySegment<T>.AsSequence(memory, bufferSize, emptySegmentFreq);
            items.AddRange(CsvReader.Read<T, Obj>(memory, options));
        }

        Validate(items, escaping);
    }

    [Theory, MemberData(nameof(SyncTypeParams))]
    public async Task Records_Sync(
        string newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        int emptySegmentFreq,
        Mode escaping)
    {
        newline = newline == "LF" ? "\n" : "\r\n";
        CsvOptions<T> options = PrepareOptions(newline, header, escaping);

        List<Obj> items;

        using (var writer = CsvReaderTestsBase<T>.GetWriter(newline, header, trailingLF, escaping))
        using (GetMemory(writer, out var memory))
        {
            var sequence = MemorySegment<T>.AsSequence(memory, bufferSize, emptySegmentFreq);

            CsvRecordEnumerable<T> enumerable = CsvReader.Enumerate(sequence, options);

            using (var enumerator = enumerable.GetEnumerator())
            {
                items = await GetItems(() => new(enumerator.MoveNext() ? enumerator.Current : null), header);
            }
        }

        Validate(items, escaping);
    }

    [Theory, MemberData(nameof(AsyncTypeParams))]
    public async Task Objects_Async(
        string newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Mode escaping)
    {
        newline = newline == "LF" ? "\n" : "\r\n";
        CsvOptions<T> options = PrepareOptions(newline, header, escaping);

        List<Obj> items = new(1000);

        using (var writer = CsvReaderTestsBase<T>.GetWriter(newline, header, trailingLF, escaping))
        using (var owner = GetMemoryOwner(writer))
        using (var stream = owner.AsStream())
        {
            await foreach (var obj in GetObjects(stream, options, bufferSize))
            {
                items.Add(obj);
            }
        }

        Validate(items, escaping);
    }

    [Theory, MemberData(nameof(AsyncTypeParams))]
    public async Task Records_Async(
        string newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Mode escaping)
    {
        newline = newline == "LF" ? "\n" : "\r\n";
        CsvOptions<T> options = PrepareOptions(newline, header, escaping);

        List<Obj> items;

        using (var writer = CsvReaderTestsBase<T>.GetWriter(newline, header, trailingLF, escaping))
        using (var owner = GetMemoryOwner(writer))
        using (var stream = owner.AsStream())
        {
            CsvRecordAsyncEnumerable<T> enumerable = GetRecords(stream, options, bufferSize);

            using (var enumerator = enumerable.GetAsyncEnumerator())
            {
                items = await GetItems(async () => await enumerator.MoveNextAsync() ? enumerator.Current : null, header);
            }
        }

        Validate(items, escaping);
    }

    private static void Validate(List<Obj> items, Mode escaping)
    {
        Assert.Equal(1_000, items.Count);

        for (int i = 0; i < 1_000; i++)
        {
            var obj = items[i];
            Assert.Equal(i, obj.Id);
            Assert.Equal(escaping != Mode.None ? $"Name\"{i}" : $"Name-{i}", obj.Name);
            Assert.Equal(i % 2 == 0, obj.IsEnabled);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(i), obj.LastLogin);
            Assert.Equal(new Guid(i, 0, 0, TestDataGenerator._guidbytes), obj.Token);
        }
    }

    protected static MemoryOwner<byte> GetMemoryOwner(ArrayPoolBufferWriter<char> writer)
    {
        var owner = MemoryOwner<byte>.Allocate(Encoding.UTF8.GetByteCount(writer.WrittenSpan));
        Assert.Equal(owner.Length, Encoding.UTF8.GetBytes(writer.WrittenSpan, owner.Span));
        return owner;
    }

    private static async Task<List<Obj>> GetItems(
        Func<ValueTask<CsvValueRecord<T>?>> enumerator,
        bool hasHeader)
    {
        List<Obj> items = new(1000);

        int index = 0;
        long tokenPosition = 0;

        while (await enumerator() is { } record)
        {
            if (tokenPosition == 0 && hasHeader)
            {
                tokenPosition = TestDataGenerator.Header.Length + record.Dialect.Newline.Length;
            }

            index++;
            Assert.Equal(hasHeader ? index + 1 : index, record.Line);
            Assert.Equal(tokenPosition, record.Position);

            tokenPosition += record.RawRecord.Length + record.Dialect.Newline.Length;

            items.Add(
                new Obj
                {
                    Id = record.GetField<int>(0),
                    Name = record.GetField<string?>(1),
                    IsEnabled = record.GetField<bool>(2),
                    LastLogin = record.GetField<DateTimeOffset>(3),
                    Token = record.GetField<Guid>(4),
                });

            Assert.Equal(5, record.GetFieldCount());
        }

        return items;
    }

    private CsvOptions<T> PrepareOptions(string newline, bool header, Mode escaping)
    {
        CsvOptions<T> options = CreateOptions(
            newline,
            escaping == Mode.Escape ? '^' : null);

        options.HasHeader = header;
        options.AllowContentInExceptions = true;
        options.ArrayPool = _pool = new() { TrackStackTraces = false };

        return options;
    }

    private static ArrayPoolBufferWriter<char> GetWriter(string newline, bool header, bool trailingLF, Mode escaping)
    {
        var writer = new ArrayPoolBufferWriter<char>(ushort.MaxValue + short.MaxValue);
        TestDataGenerator.Generate(writer, newline, header, trailingLF, escaping);
        return writer;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>")]
    public void Dispose()
    {
        _pool?.Dispose();
    }
}
