using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Reading;
using FlameCsv.Tests.Utilities;

namespace FlameCsv.Tests.Readers;

public class RFC4180NativeFromStartTests : RFC4180NativeTestsBase
{
    protected override bool FromEnd => false;
}

public class RFC4180NativeFromEndTests : RFC4180NativeTestsBase
{
    protected override bool FromEnd => true;
}

[SupportedOSPlatform("windows")]
public abstract class RFC4180NativeTestsBase : RFC4180ModeTests, IDisposable
{
    protected abstract bool FromEnd { get; }

    private MemoryManager<char>? _inputManager;
    private MemoryManager<char>? _scratchManager;

    public void Dispose()
    {
        (_inputManager as IDisposable)?.Dispose();
        (_scratchManager as IDisposable)?.Dispose();
    }

    protected override Span<char> AllocateScratch(int minLength)
    {
        if (_scratchManager is not null)
            ThrowConcurrentUsage();

        _scratchManager = new GuardedMemoryManager<char>(minLength, FromEnd);
        return _scratchManager.GetSpan();
    }

    protected override ReadOnlyMemory<char> AllocateMemory(string input)
    {
        if (_inputManager is not null)
            ThrowConcurrentUsage();

        _inputManager = new GuardedMemoryManager<char>(input.Length, FromEnd);

        var memory = _inputManager.Memory;
        input.AsMemory().CopyTo(memory);
        return memory[..input.Length];
    }

    [DoesNotReturn]
    private static void ThrowConcurrentUsage()
    {
        throw new InvalidOperationException("Concurrent usage detected");
    }
}

public class RFC4180StringTests : RFC4180ModeTests
{
    protected override ReadOnlyMemory<char> AllocateMemory(string input) => input.AsMemory();
    protected override Span<char> AllocateScratch(int minLength) => new char[minLength];
}

public abstract class RFC4180ModeTests
{
    protected abstract ReadOnlyMemory<char> AllocateMemory(string input);
    protected abstract Span<char> AllocateScratch(int minLength);

    [Fact]
    public void Aasdasd()
    {
        var _bytes = File.ReadAllText(
            "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv",
            Encoding.UTF8);
        SearchValues<char> searchValues = SearchValues.Create(",\"\r\n");

        Span<char> unescapeBuffer = stackalloc char[256];
        Span<Meta> metaBuffer = new Meta[1024];
        ReadOnlySpan<char> bytes = _bytes;
        ref readonly var dialect = ref CsvOptions<char>.Default.Dialect;

        int count;

        while (true)
        {
            count = Buffah<char>.Read(bytes, metaBuffer, in dialect, searchValues, false);

            if (count == 0)
            {
                break;
            }

            for (int i = 0; i < count; i++)
            {
                var meta = metaBuffer[i];
                var line = meta.SliceUnsafe(in dialect, bytes, unescapeBuffer);
                var str = line.ToString();
                _ = 1;
            }

            var last = metaBuffer[count - 1];
            bytes = bytes.Slice(last.GetStartOfNext(2));
        }

        count = Buffah<char>.Read(bytes, metaBuffer, in dialect, searchValues, true);

        for (int i = 0; i < count; i++)
        {
            var meta = metaBuffer[i];
            var line = meta.SliceUnsafe(in dialect, bytes, unescapeBuffer);
            var str = line.ToString();
            _ = 1;
        }
    }

    [Fact]
    public void AAAAAAA()
    {
        const string data =
            "id,name,isenabled\r\n1,\"Bob\",true\r\n2,\"Alice\",false\r\n";

        var searchValues = SearchValues.Create(",\"\r\n");
        var buffer = new Meta[16];
        Span<char> scratch = stackalloc char[128];

        int read = Buffah<char>.Read(data, buffer, in CsvOptions<char>.Default.Dialect, searchValues, false);

        List<string> items = [];

        for (int i = 0; i < read; i++)
        {
            var slice = buffer[i];
            var span = slice.SliceUnsafe(in CsvOptions<char>.Default.Dialect, data, scratch);
            var field = span.ToString();
            items.Add(field);
        }

        string[] expected = ["id", "name", "isenabled", "1", "Bob", "true", "2", "Alice", "false",];

        Assert.Equal(expected, items);
    }


    [Theory]
    [InlineData("test", "test")]
    [InlineData(" test", "test")]
    [InlineData("test ", "test")]
    [InlineData(" test ", "test")]
    [InlineData("\" test\"", "test")]
    [InlineData("\"test \"", "test")]
    [InlineData("\" test \"", "test")]
    public void Should_Trim_Fields(string input, string expected)
    {
        using var parser = CsvParser<char>.Create(new CsvOptions<char> { Whitespace = " " });

        IMemoryOwner<char>? allocated = null;

        var memory = AllocateMemory(input);

        var line = parser.GetAsCsvLine(memory);
        var reader = new CsvFieldReader<char>(
            parser.Options,
            in line,
            AllocateScratch(16),
            ref allocated);


        Assert.True(reader.MoveNext());
        Assert.Equal(expected, reader.Current.ToString());
        Assert.True(reader.End);
        allocated?.Dispose();
    }

    [Fact]
    public void Should_Seek_Long_Line()
    {
        const string input = "\"Long line with lots of content, but no quotes except the wrapping!\"";
        using var parser = CsvParser<char>.Create(CsvOptions<char>.Default);

        var line = parser.GetAsCsvLine(AllocateMemory(input));
        var reader = new CsvFieldReader<char>(
            parser.Options,
            in line,
            [],
            ref Unsafe.NullRef<IMemoryOwner<char>?>());

        Assert.True(reader.MoveNext());
        Assert.Equal(input[1..^1], reader.Current.ToString());
        Assert.True(reader.End);
    }

    [Theory]
    [InlineData("\"test\"", "test")]
    [InlineData("\"\"", "")]
    [InlineData("\"te\"\"st\"", "te\"st")]
    [InlineData("\"test\"\"\"", "test\"")]
    [InlineData("\"\"\"test\"\"\"", "\"test\"")]
    [InlineData("\"\"\"\"", "\"")]
    [InlineData("\"Some long, sentence\"", "Some long, sentence")]
    [InlineData("\"James \"\"007\"\" Bond\"", "James \"007\" Bond")]
    public void Should_Unescape(string input, string expected)
    {
        using var parser = CsvParser<char>.Create(CsvOptions<char>.Default);

        IMemoryOwner<char>? allocated = null;

        var line = parser.GetAsCsvLine(AllocateMemory(input));
        var record = new CsvFieldReader<char>(
            parser.Options,
            in line,
            AllocateScratch(128),
            ref allocated);

        var field = RFC4180Mode<char>.ReadNextField(ref record);

        Assert.Equal(expected, field.ToString());
        allocated?.Dispose();
    }

    [Theory]
    [InlineData(",,,,")]
    [InlineData("a,b,c,d,e")]
    [InlineData("x,y,asdalksdjasd,,")]
    [InlineData(",jklsadklasdW,laskdjlksad,,1231")]
    [InlineData("A,\"B\",C,D,E")]
    [InlineData("A,\"B\",C,D,\"E\"")]
    public void Should_Enumerate_Fields(string input)
    {
        using var pool = new ReturnTrackingArrayMemoryPool<char>();
        var options = new CsvOptions<char> { Newline = "|", MemoryPool = pool, };

        var expected = input.Split(',').Select(s => s.Trim('"'));

        var list = new List<string>();
        using var parser = CsvParser<char>.Create(options);

        IMemoryOwner<char>? allocated = null;
        var line = parser.GetAsCsvLine(AllocateMemory(input));
        CsvFieldReader<char> reader = new(
            parser.Options,
            in line,
            [],
            ref allocated);

        while (!reader.End)
        {
            list.Add(RFC4180Mode<char>.ReadNextField(ref reader).ToString());
        }

        Assert.Equal(expected, list);
        allocated?.Dispose();
    }

    [Fact]
    public void Should_Enumerate_With_Comma2()
    {
        using var pool = new ReturnTrackingArrayMemoryPool<char>();
        var options = new CsvOptions<char> { Newline = "|", MemoryPool = pool, };

        var data = new[] { options.Delimiter, options.Newline[0] }.GetPermutations();
        IMemoryOwner<char>? allocated = null;

        foreach (var chars in data)
        {
            var input = new string(chars.ToArray());
            var str = $"\"{input}\",test";
            var line = new CsvLine<char> { Value = str.AsMemory(), QuoteCount = (uint)str.Count('"') };

            CsvFieldReader<char> state = new(
                options,
                in line,
                [],
                ref allocated);

            var list = new List<string>();

            while (!state.End)
            {
                list.Add(RFC4180Mode<char>.ReadNextField(ref state).ToString());
            }

            Assert.Equal(2, list.Count);
            Assert.Equal(input, list[0]);
            Assert.Equal("test", list[1]);
        }

        allocated?.Dispose();
    }

    [Fact]
    public void Should_Handle_Segment_With_Only_CarriageReturn()
    {
        using var parser = CsvParser<char>.Create(CsvOptions<char>.Default);
        IMemoryOwner<char>? allocated = null;

        var data = MemorySegment.Create(
            "some,line,here\r",
            "\r\r",
            "\r",
            "\n");

        parser.Reset(in data);
        Assert.True(parser.TryReadLine(out var line, false));

        Assert.Equal("some,line,here\r\r\r\r", line.ToString());

        allocated?.Dispose();
    }
}
