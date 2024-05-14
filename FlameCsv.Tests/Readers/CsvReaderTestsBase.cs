using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Tests.TestData;
using FlameCsv.Tests.Utilities;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

/// <summary>
/// A spray-and-pray tests of different APIs using various options and CSV features.
/// </summary>
[Collection("ReaderTests")]
public abstract class CsvReaderTestsBase<T> : IDisposable
    where T : unmanaged, IEquatable<T>
{
    private static readonly int[] _bufferSizes = [-1, 17, 128, 1024, 8096];
    private static readonly int[] _emptySegmentsEvery = [0, 1, 7];
    private static readonly string[] _crlf = ["CRLF", "LF"];
    private static readonly bool[] _booleans = [true, false];
    private static readonly Mode[] _escaping = [Mode.None, Mode.RFC, Mode.Escape];

    private ReturnTrackingArrayPool<T>? _pool;

    protected abstract CsvTypeMap<T, Obj> TypeMap { get; }
    protected abstract CsvOptions<T> CreateOptions(string newline, char? escape);

    protected abstract CsvRecordAsyncEnumerable<T> GetRecords(
        Stream stream,
        CsvOptions<T> options,
        int bufferSize);
    protected abstract IAsyncEnumerable<Obj> GetObjects(
        Stream stream,
        CsvOptions<T> options,
        int bufferSize,
        bool sourceGen);

    public static IEnumerable<object[]> SyncObjectParams => SyncParams.Where(x => x[1] is true || x[^1] is false);
    public static IEnumerable<object[]> SyncRecordParams => SyncParams.Where(x => x[1] is true || x[^1] is false);

    private static IEnumerable<object[]> SyncParams
        =>
            from crlf in _crlf
            from writeHeader in _booleans
            from writeTrailingNewline in _booleans
            from bufferSize in _bufferSizes
            from emptySegmentFrequency in _emptySegmentsEvery
            from escaping in _escaping
            from sourceGen in _booleans
            select new object[] { crlf, writeHeader, writeTrailingNewline, bufferSize, emptySegmentFrequency, escaping, sourceGen };

    public static IEnumerable<object[]> AsyncObjectParams => AsyncParams.Where(x => x[1] is true || x[^1] is false);
    public static IEnumerable<object[]> AsyncRecordParams => AsyncParams.Where(x => x[1] is true || x[^1] is false);

    private static IEnumerable<object[]> AsyncParams
        =>
            from crlf in _crlf
            from writeHeader in _booleans
            from writeTrailingNewline in _booleans
            from bufferSize in _bufferSizes
            from escaping in _escaping
            from sourceGen in _booleans
            select new object[] { crlf, writeHeader, writeTrailingNewline, bufferSize, escaping, sourceGen };

    [Theory, MemberData(nameof(SyncObjectParams))]
    public void Objects_Sync(
        string newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        int emptySegmentFreq,
        Mode escaping,
        bool sourceGen)
    {
        newline = newline == "LF" ? "\n" : "\r\n";
        CsvOptions<T> options = PrepareOptions(newline, header, escaping);

        List<Obj> items = new(1000);

        var memory = TestDataGenerator.Generate<T>(newline, header, trailingLF, escaping);
        var sequence = MemorySegment<T>.AsSequence(memory, bufferSize, emptySegmentFreq);

        if (sourceGen)
            items.AddRange(CsvReader.Read(sequence, TypeMap, options));
        else
            items.AddRange(CsvReader.Read<T, Obj>(sequence, options));

        Validate(items, escaping);
    }

    [Theory, MemberData(nameof(SyncRecordParams))]
    public async Task Records_Sync(
        string newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        int emptySegmentFreq,
        Mode escaping,
        bool sourceGen)
    {
        newline = newline == "LF" ? "\n" : "\r\n";
        CsvOptions<T> options = PrepareOptions(newline, header, escaping);

        List<Obj> items;

        var memory = TestDataGenerator.Generate<T>(newline, header, trailingLF, escaping);
        var sequence = MemorySegment<T>.AsSequence(memory, bufferSize, emptySegmentFreq);

        CsvRecordEnumerable<T> enumerable = CsvReader.Enumerate(sequence, options);

        using (var enumerator = enumerable.GetEnumerator())
        {
            items = await GetItems(() => new(enumerator.MoveNext() ? enumerator.Current : null), sourceGen, header);
        }

        Validate(items, escaping);
    }

    [Theory, MemberData(nameof(AsyncObjectParams))]
    public async Task Objects_Async(
        string newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Mode escaping,
        bool sourceGen)
    {
        newline = newline == "LF" ? "\n" : "\r\n";
        CsvOptions<T> options = PrepareOptions(newline, header, escaping);

        List<Obj> items = new(1000);

        var data = TestDataGenerator.Generate<byte>(newline, header, trailingLF, escaping);

        await using (var stream = data.AsStream())
        {
            await foreach (var obj in GetObjects(stream, options, bufferSize, sourceGen))
            {
                items.Add(obj);
            }
        }

        Validate(items, escaping);
    }

    [Theory, MemberData(nameof(AsyncRecordParams))]
    public async Task Records_Async(
        string newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Mode escaping,
        bool sourceGen)
    {
        newline = newline == "LF" ? "\n" : "\r\n";
        CsvOptions<T> options = PrepareOptions(newline, header, escaping);

        List<Obj> items;

        var data = TestDataGenerator.Generate<byte>(newline, header, trailingLF, escaping);

        await using (var stream = data.AsStream())
        {
            CsvRecordAsyncEnumerable<T> enumerable = GetRecords(stream, options, bufferSize);

            await using var enumerator = enumerable.GetAsyncEnumerator();
            items = await GetItems(
                async () => await enumerator.MoveNextAsync() ? enumerator.Current : null,
                sourceGen,
                header);
        }

        Validate(items, escaping);
    }

    private static void Validate(List<Obj> items, Mode escaping)
    {
        if (items.Count != 1000)
        {
            var missing = Enumerable.Range(0, 1_000).ToHashSet();

            foreach (var obj in items)
                missing.Remove(obj.Id);

            Assert.Empty(missing);
        }

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

    private async Task<List<Obj>> GetItems(
        Func<ValueTask<CsvValueRecord<T>?>> enumerator,
        bool sourceGen,
        bool hasHeader)
    {
        List<Obj> items = new(1000);

        int index = 0;
        long tokenPosition = 0;

        while (await enumerator() is { } record)
        {
            if (tokenPosition == 0 && hasHeader)
            {
                tokenPosition = TestDataGenerator.Header.Length + record._options._newline.Length;
            }

            index++;
            Assert.Equal(hasHeader ? index + 1 : index, record.Line);
            Assert.Equal(tokenPosition, record.Position);

            tokenPosition += record.RawRecord.Length + record._options._newline.Length;

            Obj obj = new Obj
            {
                Id = record.GetField<int>(0),
                Name = record.GetField<string?>(1),
                IsEnabled = record.GetField<bool>(2),
                LastLogin = record.GetField<DateTimeOffset>(3),
                Token = record.GetField<Guid>(4),
            };

            var parsed = sourceGen ? record.ParseRecord<Obj>(TypeMap) : record.ParseRecord<Obj>();
            Assert.Equal(obj, parsed);

            items.Add(obj);

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
        options.ExceptionHandler = (in CsvExceptionHandlerArgs<T> args) =>
        {
            if (Debugger.IsAttached)
            {
                var str = args.Options.GetAsString(args.Record.Span);
            }

            return false;
        };

        return options;
    }

    public void Dispose()
    {
        _pool?.Dispose();
    }
}
