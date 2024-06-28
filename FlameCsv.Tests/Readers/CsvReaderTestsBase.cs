using System.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Tests.TestData;
using FlameCsv.Tests.Utilities;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable LoopCanBeConvertedToQuery

namespace FlameCsv.Tests.Readers;

public enum NewlineToken { CRLF, LF, AutoCRLF, AutoLF }

/// <summary>
/// A spray-and-pray tests of different APIs using various options and CSV features.
/// </summary>
//[Collection("ReaderTests")]
public abstract class CsvReaderTestsBase<T> : IDisposable
    where T : unmanaged, IEquatable<T>
{
    private static readonly int[] _bufferSizes = [-1, 17, 128, 1024, 8096];
    private static readonly int[] _emptySegmentsEvery = [0, 1, 7];
    private static readonly NewlineToken[] _crlf = [NewlineToken.CRLF, NewlineToken.LF, NewlineToken.AutoCRLF, NewlineToken.AutoLF];
    private static readonly bool[] _booleans = [true, false];
    private static readonly Mode[] _escaping = [Mode.None, Mode.RFC, Mode.Escape];

    private ReturnTrackingArrayPool<T>? _pool;

    protected abstract CsvTypeMap<T, Obj> TypeMap { get; }
    protected abstract CsvOptions<T> CreateOptions(NewlineToken newline, char? escape);

    protected abstract CsvRecordAsyncEnumerable<T> GetRecords(
        Stream stream,
        CsvOptions<T> options,
        int bufferSize);
    protected abstract IAsyncEnumerable<Obj> GetObjects(
        Stream stream,
        CsvOptions<T> options,
        int bufferSize,
        bool sourceGen);

    public static TheoryData<NewlineToken, bool, bool, int, int, Mode, bool> SyncParams
    {
        get
        {
            var values = from crlf in _crlf
                         from writeHeader in _booleans
                         from writeTrailingNewline in _booleans
                         from bufferSize in _bufferSizes
                         from emptySegmentFrequency in _emptySegmentsEvery
                         from escaping in _escaping
                         from sourceGen in _booleans
                         select new { crlf, writeHeader, writeTrailingNewline, bufferSize, emptySegmentFrequency, escaping, sourceGen };

            var data = new TheoryData<NewlineToken, bool, bool, int, int, Mode, bool>();

            foreach (var x in values)
            {
                // headerless csv not yet supported on sourcegen
                if (x.sourceGen && !x.writeHeader)
                    continue;

                data.Add(x.crlf, x.writeHeader, x.writeTrailingNewline, x.bufferSize, x.emptySegmentFrequency, x.escaping, x.sourceGen);
            }

            return data;
        }
    }

    public static TheoryData<NewlineToken, bool, bool, int, Mode, bool> AsyncParams
    {
        get
        {
            var values = from crlf in _crlf
                         from writeHeader in _booleans
                         from writeTrailingNewline in _booleans
                         from bufferSize in _bufferSizes
                         from escaping in _escaping
                         from sourceGen in _booleans
                         select new { crlf, writeHeader, writeTrailingNewline, bufferSize, escaping, sourceGen };

            var data = new TheoryData<NewlineToken, bool, bool, int, Mode, bool>();

            foreach (var x in values)
            {
                // headerless csv not yet supported on sourcegen
                if (x.sourceGen && !x.writeHeader)
                    continue;

                data.Add(x.crlf, x.writeHeader, x.writeTrailingNewline, x.bufferSize, x.escaping, x.sourceGen);
            }

            return data;
        }
    }

    [Theory, MemberData(nameof(SyncParams))]
    public void Objects_Sync(
        NewlineToken newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        int emptySegmentFreq,
        Mode escaping,
        bool sourceGen)
    {
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

    [Theory, MemberData(nameof(SyncParams))]
    public async Task Records_Sync(
        NewlineToken newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        int emptySegmentFreq,
        Mode escaping,
        bool sourceGen)
    {
        CsvOptions<T> options = PrepareOptions(newline, header, escaping);

        List<Obj> items;

        var memory = TestDataGenerator.Generate<T>(newline, header, trailingLF, escaping);
        var sequence = MemorySegment<T>.AsSequence(memory, bufferSize, emptySegmentFreq);

        CsvRecordEnumerable<T> enumerable = CsvReader.Enumerate(sequence, options);

        using (var enumerator = enumerable.GetEnumerator())
        {
            items = await GetItems(() => new(enumerator.MoveNext() ? enumerator.Current : null), sourceGen, header, newline);
        }

        Validate(items, escaping);
    }

    [Theory, MemberData(nameof(AsyncParams))]
    public async Task Objects_Async(
        NewlineToken newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Mode escaping,
        bool sourceGen)
    {
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

    [Theory, MemberData(nameof(AsyncParams))]
    public async Task Records_Async(
        NewlineToken newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        Mode escaping,
        bool sourceGen)
    {
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
                header,
                newline);
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
        bool hasHeader,
        NewlineToken newline)
    {
        List<Obj> items = new(1000);

        int index = 0;
        long tokenPosition = 0;

        int newlineLength = newline switch
        {
            NewlineToken.LF or NewlineToken.AutoLF => 1,
            _ => 2,
        };

        while (await enumerator() is { } record)
        {
            if (tokenPosition == 0 && hasHeader)
            {
                tokenPosition = TestDataGenerator.Header.Length + newlineLength;
            }

            index++;
            Assert.Equal(hasHeader ? index + 1 : index, record.Line);
            Assert.Equal(tokenPosition, record.Position);

            tokenPosition += record.RawRecord.Length + newlineLength;

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

    private CsvOptions<T> PrepareOptions(NewlineToken newline, bool header, Mode escaping)
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
