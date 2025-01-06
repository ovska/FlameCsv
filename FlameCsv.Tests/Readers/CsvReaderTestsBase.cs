using System.Buffers;
using System.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Enumeration;
using FlameCsv.Tests.TestData;
using FlameCsv.Tests.Utilities;

// ReSharper disable InconsistentNaming

namespace FlameCsv.Tests.Readers;

public enum NewlineToken { CRLF, LF, AutoCRLF, AutoLF }

public abstract class CsvReaderTestsBase
{
    protected static readonly int[] _bufferSizes = [-1, 17, 128, 1024, 8096];
    protected static readonly int[] _emptySegmentsEvery = [0, 1, 7];
    protected static readonly NewlineToken[] _crlf = [NewlineToken.CRLF, NewlineToken.LF, NewlineToken.AutoCRLF, NewlineToken.AutoLF];
    protected static readonly bool[] _booleans = [true, false];
    protected static readonly Mode[] _escaping = [Mode.None, Mode.RFC, Mode.Escape];
}

/// <summary>
/// A spray-and-pray tests of different APIs using various options and CSV features.
/// </summary>
//[Collection("ReaderTests")]
public abstract class CsvReaderTestsBase<T> : CsvReaderTestsBase, IDisposable where T : unmanaged, IEquatable<T>
{
    private readonly MemoryPool<T> _pool = Debugger.IsAttached ? ReturnTrackingMemoryPool<T>.Shared : MemoryPool<T>.Shared;

    protected abstract CsvTypeMap<T, Obj> TypeMap { get; }

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
    public async Task Objects_Sync(
        NewlineToken newline,
        bool header,
        bool trailingLF,
        int bufferSize,
        int emptySegmentFreq,
        Mode escaping,
        bool sourceGen)
    {
        await Validate(Enumerate(), escaping);

        IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, escaping);

            var memory = TestDataGenerator.Generate<T>(newline, header, trailingLF, escaping);
            var sequence = MemorySegment<T>.AsSequence(memory, bufferSize, emptySegmentFreq);

            return SyncAsyncEnumerable.Create<Obj>(
                sourceGen
                    ? CsvReader.Read(sequence, TypeMap, options)
                    : CsvReader.Read<T, Obj>(sequence, options));
        }
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
        await Validate(Enumerate(), escaping);

        IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, escaping);

            var memory = TestDataGenerator.Generate<T>(newline, header, trailingLF, escaping);
            var sequence = MemorySegment<T>.AsSequence(memory, bufferSize, emptySegmentFreq);

            var items = GetItems(
                SyncAsyncEnumerable.Create(CsvReader.Enumerate(sequence, options)),
                sourceGen,
                header,
                newline);

            return items;
        }
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
        await Validate(Enumerate(), escaping);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, escaping);

            var data = TestDataGenerator.Generate<byte>(newline, header, trailingLF, escaping);

            await using var stream = data.AsStream();
            await foreach (var obj in GetObjects(stream, options, bufferSize, sourceGen).ConfigureAwait(false))
            {
                yield return obj;
            }
        }
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
        await Validate(Enumerate(), escaping);

        async IAsyncEnumerable<Obj> Enumerate()
        {
            CsvOptions<T> options = GetOptions(newline, header, escaping);

            var data = TestDataGenerator.Generate<byte>(newline, header, trailingLF, escaping);

            await using var stream = data.AsStream();
            var items = GetItems(GetRecords(stream, options, bufferSize), sourceGen, header, newline);
            await foreach (var item in items.ConfigureAwait(false))
            {
                yield return item;
            }
        }
    }

    private static async Task Validate(IAsyncEnumerable<Obj> enumerable, Mode escaping)
    {
        int i = 0;

        await foreach (var obj in enumerable.ConfigureAwait(false))
        {
            Assert.Equal(i, obj.Id);
            Assert.Equal(escaping != Mode.None ? $"Name\"{i}" : $"Name-{i}", obj.Name);
            Assert.Equal(i % 2 == 0, obj.IsEnabled);
            Assert.Equal(DateTimeOffset.UnixEpoch.AddDays(i), obj.LastLogin);
            Assert.Equal(new Guid(i, 0, 0, TestDataGenerator.GuidBytes), obj.Token);

            i++;
        }

        Assert.Equal(1_000, i);
    }

    private async IAsyncEnumerable<Obj> GetItems(
        IAsyncEnumerable<CsvValueRecord<T>> enumerable,
        bool sourceGen,
        bool hasHeader,
        NewlineToken newline)
    {
        int index = 0;
        long tokenPosition = 0;

        int newlineLength = newline switch
        {
            NewlineToken.LF or NewlineToken.AutoLF => 1,
            _ => 2,
        };

        await foreach (var record in enumerable.ConfigureAwait(false))
        {
            if (tokenPosition == 0 && hasHeader)
            {
                tokenPosition = TestDataGenerator.Header.Length + newlineLength;
            }

            index++;
            Assert.Equal(hasHeader ? index + 1 : index, record.Line);
            Assert.Equal(tokenPosition, record.Position);

            tokenPosition += record.RawRecord.Length + newlineLength;

            Obj obj = new()
            {
                Id = record.GetField<int>(0),
                Name = record.GetField<string?>(1),
                IsEnabled = record.GetField<bool>(2),
                LastLogin = record.GetField<DateTimeOffset>(3),
                Token = record.GetField<Guid>(4),
            };

            var parsed = sourceGen ? record.ParseRecord(TypeMap) : record.ParseRecord<Obj>();
            Assert.Equal(obj, parsed);

            Assert.Equal(5, record.GetFieldCount());

            yield return obj;
        }
    }

    private CsvOptions<T> GetOptions(NewlineToken newline, bool header, Mode escaping)
    {
        return new CsvOptions<T>
        {
            Formats = { [typeof(DateTime)] = "O" },
            Escape = escaping == Mode.Escape ? '^' : null,
            Newline = newline switch
            {
                NewlineToken.LF => "\n",
                NewlineToken.CRLF => "\r\n",
                _ => default,
            },
            HasHeader = header,
            AllowContentInExceptions = true,
            MemoryPool = _pool,
#if false
            ExceptionHandler = static (in CsvExceptionHandlerArgs<T> args) =>
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    var str = args.Options.GetAsString(args.Record.Span);
                }

                return false;
            },
#endif
        };
    }

    public void Dispose()
    {
        (_pool as ReturnTrackingMemoryPool<T>)?.Dispose();
    }
}
