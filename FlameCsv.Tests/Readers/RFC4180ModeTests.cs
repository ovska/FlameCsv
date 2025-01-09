using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
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

        var meta = parser.GetRecordMeta(memory.Span);
        var reader = new CsvFieldReader<char>(
            parser.Options,
            memory.Span,
            AllocateScratch(16),
            ref allocated,
            in meta);


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

        var memory = AllocateMemory(input);
        var meta = parser.GetRecordMeta(memory.Span);
        var reader = new CsvFieldReader<char>(
            parser.Options,
            memory.Span,
            [],
            ref Unsafe.NullRef<IMemoryOwner<char>?>(),
            in meta);

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

        var meta = new CsvRecordMeta { quoteCount = (uint)input.Count(c => c == '"') };
        var record = new CsvFieldReader<char>(
            parser.Options,
            AllocateMemory(input).Span,
            AllocateScratch(128),
            ref allocated,
            in meta);

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
    public void Should_Enumerate_Fields(string line)
    {
        using var pool = new ReturnTrackingArrayMemoryPool<char>();
        var options = new CsvOptions<char> { Newline = "|", MemoryPool = pool, AllowContentInExceptions = true, };

        var expected = line.Split(',').Select(s => s.Trim('"'));

        var list = new List<string>();
        using var parser = CsvParser<char>.Create(options);

        IMemoryOwner<char>? allocated = null;

        var memory = AllocateMemory(line);
        var meta = parser.GetRecordMeta(memory.Span);
        CsvFieldReader<char> reader = new(
            parser.Options,
            memory.Span,
            [],
            ref allocated,
            in meta);

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
        var options = new CsvOptions<char> { Newline = "|", MemoryPool = pool, AllowContentInExceptions = true, };

        var data = new[] { options.Delimiter, options.Newline[0] }.GetPermutations();
        IMemoryOwner<char>? allocated = null;

        foreach (var chars in data)
        {
            var input = new string(chars.ToArray());
            var line = $"\"{input}\",test";

            var meta = new CsvRecordMeta { quoteCount = (uint)line.Count('"') };
            CsvFieldReader<char> state = new(
                options,
                line,
                [],
                ref allocated,
                in meta);

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
        Assert.True(parser.TryReadLine(out var line, out _, false));

        Assert.Equal("some,line,here\r\r\r\r", line.ToString());

        allocated?.Dispose();
    }
}
