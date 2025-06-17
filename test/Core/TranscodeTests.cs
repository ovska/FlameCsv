using System.Runtime.InteropServices;
using System.Text;

namespace FlameCsv.Tests;

public static class TranscodeTests
{
    [Fact]
    public static void InvalidType()
    {
        Assert.Throws<NotSupportedException>(() => Transcode.TryFromChars([], Span<int>.Empty, out _));
        Assert.Throws<NotSupportedException>(() => Transcode.TryToChars(Span<int>.Empty, [], out _));
        Assert.Throws<NotSupportedException>(() => Transcode.ToString(Span<int>.Empty));
        Assert.Throws<NotSupportedException>(() => Transcode.FromString<int>(""));
    }

    [Fact]
    public static void TryFromChars_Char()
    {
        var value = "Hello, World!";
        var destination = new char[value.Length];
        Assert.True(Transcode.TryFromChars(value, destination, out var charsWritten));
        Assert.Equal(value.Length, charsWritten);
        Assert.Equal(value, new string(destination));

        Assert.False(Transcode.TryFromChars(value, destination.AsSpan(0, destination.Length - 1), out _));
    }

    [Fact]
    public static void TryFromChars_Byte()
    {
        var value = "Hello, World!";
        var destination = new byte[Encoding.UTF8.GetByteCount(value)];
        Assert.True(Transcode.TryFromChars(value, destination, out var charsWritten));
        Assert.Equal(value.Length, charsWritten);
        Assert.Equal(value, Encoding.UTF8.GetString(destination));

        Assert.False(Transcode.TryFromChars(value, destination.AsSpan(0, destination.Length - 1), out _));
    }

    [Fact]
    public static void FromString_Char()
    {
        var value = "Hello, World!";
        var memory = Transcode.FromString<char>(value);
        Assert.Equal(value.Length, memory.Length);
        Assert.Equal(value, memory.Span.ToString());

        Assert.True(MemoryMarshal.TryGetString(memory, out var str, out int start, out int length));
        Assert.Equal(0, start);
        Assert.Equal(value.Length, length);
        Assert.Same(value, str);

        Assert.Equal(0, Transcode.FromString<char>(null).Length);
        Assert.Equal(0, Transcode.FromString<char>("").Length);
    }

    [Fact]
    public static void FromString_Byte()
    {
        var value = "Hello, World!";
        var memory = Transcode.FromString<byte>(value);
        Assert.Equal(Encoding.UTF8.GetByteCount(value), memory.Length);
        Assert.Equal(value, Encoding.UTF8.GetString(memory.Span));

        Assert.Equal(0, Transcode.FromString<byte>(null).Length);
        Assert.Equal(0, Transcode.FromString<byte>("").Length);
    }

    [Fact]
    public static void ToString_Char()
    {
        Assert.Equal("Hello, World!", Transcode.ToString("Hello, World!".AsSpan()));
        Assert.Equal("", Transcode.ToString(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public static void ToString_Byte()
    {
        var value = "Hello, World!";
        var bytes = Encoding.UTF8.GetBytes(value);
        Assert.Equal(value, Transcode.ToString(bytes.AsSpan()));
        Assert.Equal("", Transcode.ToString(ReadOnlySpan<byte>.Empty));
    }
}
