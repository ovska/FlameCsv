using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.IO;

namespace FlameCsv.Tests.IO;

public class Utf8StreamReaderTests
{
    private static Utf8StreamReader CreateReader(
        string content,
        int bufferSize = 1024)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var options = new CsvIOOptions { BufferSize = bufferSize };
        return new Utf8StreamReader(stream, MemoryPool<char>.Shared, options);
    }

    [Theory]
    [InlineData("TestData", 1024)] // Standard ASCII, fits in buffer
    [InlineData("TestData", 4)] // Standard ASCII, requires multiple reads
    [InlineData("Test\nData", 1024)] // ASCII with newline
    [InlineData("Test\nData", 5)] // ASCII with newline, boundary check
    public static void Should_Read_Ascii(string content, int bufferSize)
    {
        using var reader = CreateReader(content, bufferSize);
        var charBuffer = new char[content.Length * 2]; // Ample space
        int totalCharsRead = 0;
        int charsRead;

        do
        {
            charsRead = reader.ReadCore(charBuffer.AsMemory(totalCharsRead));
            totalCharsRead += charsRead;
        } while (charsRead > 0);

        Assert.Equal(content.Length, totalCharsRead);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead));

        // Ensure reading again returns 0
        Assert.Equal(0, reader.ReadCore(charBuffer));
    }

    [Theory]
    [InlineData("你好世界", 1024)] // Multi-byte UTF-8, fits in buffer (each char is 3 bytes)
    [InlineData("你好世界", 5)] // Multi-byte UTF-8, requires multiple reads, boundary check
    [InlineData("Test你好世界Data", 1024)] // Mixed ASCII and multi-byte
    [InlineData("Test你好世界Data", 8)] // Mixed, boundary check
    public static void Should_Read_MultiByte_Utf8(string content, int bufferSize)
    {
        using var reader = CreateReader(content, bufferSize);
        var charBuffer = new char[content.Length * 2]; // Ample space
        int totalCharsRead = 0;
        int charsRead;

        do
        {
            charsRead = reader.ReadCore(charBuffer.AsMemory(totalCharsRead));
            totalCharsRead += charsRead;
        } while (charsRead > 0);

        Assert.Equal(content.Length, totalCharsRead);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead));
        Assert.Equal(0, reader.ReadCore(charBuffer)); // End of stream
    }

    [Fact]
    public static void Should_Handle_Small_Output_Buffer()
    {
        const string content = "This is a test string.";
        using var reader = CreateReader(content);
        var charBuffer = new char[5]; // Small output buffer
        var sb = new StringBuilder();
        int charsRead;

        do
        {
            charsRead = reader.ReadCore(charBuffer);
            sb.Append(charBuffer, 0, charsRead);
        } while (charsRead > 0);

        Assert.Equal(content, sb.ToString());
        Assert.Equal(0, reader.ReadCore(charBuffer)); // End of stream
    }

    [Fact]
    public static void Should_Handle_Utf8_Character_Spanning_Buffer_Boundary()
    {
        // "你好" is 6 bytes: E4 BD A0 E5 A5 BD
        // Set buffer size so the character spans the boundary
        const string content = "AB你好CD"; // 2 + 6 + 2 = 10 bytes
        using var reader = CreateReader(content, 4); // Read "AB", then part of "你", then rest + "CD"
        var charBuffer = new char[content.Length * 2];
        int totalCharsRead = 0;

        // First read should get 'A', 'B'
        int charsRead = reader.ReadCore(charBuffer.AsMemory(totalCharsRead));
        totalCharsRead += charsRead;
        Assert.True(charsRead > 0);

        // Subsequent reads
        while ((charsRead = reader.ReadCore(charBuffer.AsMemory(totalCharsRead))) > 0)
        {
            totalCharsRead += charsRead;
        }

        Assert.Equal(content.Length, totalCharsRead);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead));
        Assert.Equal(0, reader.ReadCore(charBuffer)); // End of stream
    }

    [Fact]
    public static void Should_Handle_Invalid_Utf8_Sequence()
    {
        // Valid UTF-8 for "Test", followed by an invalid byte sequence (0xFF), then "Data"
        byte[] invalidBytes = Encoding
            .UTF8.GetBytes("Test")
            .Concat(new byte[] { 0xFF })
            .Concat(Encoding.UTF8.GetBytes("Data"))
            .ToArray();

        var stream = new MemoryStream(invalidBytes);
        var options = new CsvIOOptions { BufferSize = 1024 };
        using var reader = new Utf8StreamReader(stream, MemoryPool<char>.Shared, options);

        var charBuffer = new char[20]; // Ample space
        int totalCharsRead = 0;
        int charsRead;

        do
        {
            charsRead = reader.ReadCore(charBuffer.AsMemory(totalCharsRead));
            totalCharsRead += charsRead;
        } while (charsRead > 0);

        // Expect "Test" + Replacement Character (U+FFFD) + "Data"
        string expected = "Test\uFFFDData";
        Assert.Equal(expected.Length, totalCharsRead);
        Assert.Equal(expected, new string(charBuffer, 0, totalCharsRead));
        Assert.Equal(0, reader.ReadCore(charBuffer)); // End of stream
    }

    [Fact]
    public static void Should_Handle_Invalid_Utf8_At_Buffer_End()
    {
        // Valid UTF-8 for "Test", followed by start of multi-byte char, then invalid byte
        // "€" is E2 82 AC. We'll use E2 82 FF (invalid continuation)
        byte[] invalidBytes = "Test"u8
            .ToArray()
            .Concat(new byte[] { 0xE2, 0x82, 0xFF }) // Invalid sequence starting like '€'
            .Concat("Data"u8.ToArray())
            .ToArray();

        var stream = new MemoryStream(invalidBytes);
        // Buffer size forces the invalid byte FF to be read in the next chunk
        var options = new CsvIOOptions { BufferSize = 6 }; // Reads "Test" + E2 82
        using var reader = new Utf8StreamReader(stream, MemoryPool<char>.Shared, options);

        var charBuffer = new char[20]; // Ample space
        var sb = new StringBuilder();
        int charsRead;

        do
        {
            charsRead = reader.ReadCore(charBuffer);
            sb.Append(charBuffer, 0, charsRead);
        } while (charsRead > 0);

        // Expect "Test" + Replacement Character (U+FFFD) + "Data"
        string expected = "Test\uFFFD\uFFFDData";
        Assert.Equal(expected, sb.ToString());
        Assert.Equal(0, reader.ReadCore(charBuffer)); // End of stream
    }

    [Fact]
    public static void Should_Read_Empty_Stream()
    {
        using var reader = CreateReader("");
        var charBuffer = new char[10];
        int charsRead = reader.ReadCore(charBuffer);
        Assert.Equal(0, charsRead);

        // Ensure reading again still returns 0
        charsRead = reader.ReadCore(charBuffer);
        Assert.Equal(0, charsRead);
    }

    [Fact]
    public static void Should_Read_Large_Input()
    {
        string content = string.Join(",", Enumerable.Range(0, 10000).Select(i => $"Value{i}"));
        using var reader = CreateReader(content); // Relatively small buffer
        var sb = new StringBuilder();
        var charBuffer = new char[2048]; // Read in chunks
        int charsRead;

        do
        {
            charsRead = reader.ReadCore(charBuffer);
            sb.Append(charBuffer, 0, charsRead);
        } while (charsRead > 0);

        Assert.Equal(content, sb.ToString());
        Assert.Equal(0, reader.ReadCore(charBuffer)); // End of stream
    }

    [Fact]
    public static void Should_Reset_And_Read_Again()
    {
        const string content = "First read.\nSecond read after reset.";
        using var reader = CreateReader(content);
        var charBuffer = new char[content.Length * 2];
        int totalCharsRead1 = 0;
        int charsRead;

        // First read
        do
        {
            charsRead = reader.ReadCore(charBuffer.AsMemory(totalCharsRead1));
            totalCharsRead1 += charsRead;
        } while (charsRead > 0);

        Assert.Equal(content.Length, totalCharsRead1);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead1));
        Assert.Equal(0, reader.ReadCore(charBuffer)); // End of stream

        // Reset
        Assert.True(reader.TryReset());

        // Second read
        int totalCharsRead2 = 0;
        do
        {
            charsRead = reader.ReadCore(charBuffer.AsMemory(totalCharsRead2));
            totalCharsRead2 += charsRead;
        } while (charsRead > 0);

        Assert.Equal(content.Length, totalCharsRead2);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead2));
        Assert.Equal(0, reader.ReadCore(charBuffer)); // End of stream again
    }

    [Theory]
    [InlineData("TestData", 1024)] // Standard ASCII, fits in buffer
    [InlineData("TestData", 4)] // Standard ASCII, requires multiple reads
    [InlineData("Test\nData", 1024)] // ASCII with newline
    [InlineData("Test\nData", 5)] // ASCII with newline, boundary check
    public static async Task Should_Read_Ascii_Async(string content, int bufferSize)
    {
        await using var reader = CreateReader(content, bufferSize);
        var charBuffer = new char[content.Length * 2]; // Ample space
        int totalCharsRead = 0;
        int charsRead;

        do
        {
            charsRead = await reader.ReadAsyncCore(charBuffer.AsMemory(totalCharsRead));
            totalCharsRead += charsRead;
        } while (charsRead > 0);

        Assert.Equal(content.Length, totalCharsRead);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead));

        // Ensure reading again returns 0
        Assert.Equal(0, await reader.ReadAsyncCore(charBuffer));
    }

    [Theory]
    [InlineData("你好世界", 1024)] // Multi-byte UTF-8, fits in buffer (each char is 3 bytes)
    [InlineData("你好世界", 5)] // Multi-byte UTF-8, requires multiple reads, boundary check
    [InlineData("Test你好世界Data", 1024)] // Mixed ASCII and multi-byte
    [InlineData("Test你好世界Data", 8)] // Mixed, boundary check
    public static async Task Should_Read_MultiByte_Utf8_Async(string content, int bufferSize)
    {
        await using var reader = CreateReader(content, bufferSize);
        var charBuffer = new char[content.Length * 2]; // Ample space
        int totalCharsRead = 0;
        int charsRead;

        do
        {
            charsRead = await reader.ReadAsyncCore(charBuffer.AsMemory(totalCharsRead));
            totalCharsRead += charsRead;
        } while (charsRead > 0);

        Assert.Equal(content.Length, totalCharsRead);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead));
        Assert.Equal(0, await reader.ReadAsyncCore(charBuffer)); // End of stream
    }

    [Fact]
    public static async Task Should_Handle_Small_Output_Buffer_Async()
    {
        const string content = "This is a test string.";
        await using var reader = CreateReader(content);
        var charBuffer = new char[5]; // Small output buffer
        var sb = new StringBuilder();
        int charsRead;

        do
        {
            charsRead = await reader.ReadAsyncCore(charBuffer);
            sb.Append(charBuffer, 0, charsRead);
        } while (charsRead > 0);

        Assert.Equal(content, sb.ToString());
        Assert.Equal(0, await reader.ReadAsyncCore(charBuffer)); // End of stream
    }

    [Fact]
    public static async Task Should_Handle_Utf8_Character_Spanning_Buffer_Boundary_Async()
    {
        // "你好" is 6 bytes: E4 BD A0 E5 A5 BD
        // Set buffer size so the character spans the boundary
        const string content = "AB你好CD"; // 2 + 6 + 2 = 10 bytes
        await using var reader = CreateReader(content, 4); // Read "AB", then part of "你", then rest + "CD"
        var charBuffer = new char[content.Length * 2];
        int totalCharsRead = 0;

        // First read should get 'A', 'B'
        int charsRead = await reader.ReadAsyncCore(charBuffer.AsMemory(totalCharsRead));
        totalCharsRead += charsRead;
        Assert.True(charsRead > 0);

        // Subsequent reads
        while ((charsRead = await reader.ReadAsyncCore(charBuffer.AsMemory(totalCharsRead))) > 0)
        {
            totalCharsRead += charsRead;
        }

        Assert.Equal(content.Length, totalCharsRead);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead));
        Assert.Equal(0, await reader.ReadAsyncCore(charBuffer)); // End of stream
    }

    [Fact]
    public static async Task Should_Handle_Invalid_Utf8_Sequence_Async()
    {
        // Valid UTF-8 for "Test", followed by an invalid byte sequence (0xFF), then "Data"
        byte[] invalidBytes = Encoding
            .UTF8.GetBytes("Test")
            .Concat(new byte[] { 0xFF })
            .Concat(Encoding.UTF8.GetBytes("Data"))
            .ToArray();

        var stream = new MemoryStream(invalidBytes);
        var options = new CsvIOOptions { BufferSize = 1024 };
        await using var reader = new Utf8StreamReader(stream, MemoryPool<char>.Shared, options);

        var charBuffer = new char[20]; // Ample space
        int totalCharsRead = 0;
        int charsRead;

        do
        {
            charsRead = await reader.ReadAsyncCore(charBuffer.AsMemory(totalCharsRead));
            totalCharsRead += charsRead;
        } while (charsRead > 0);

        // Expect "Test" + Replacement Character (U+FFFD) + "Data"
        string expected = "Test\uFFFDData";
        Assert.Equal(expected.Length, totalCharsRead);
        Assert.Equal(expected, new string(charBuffer, 0, totalCharsRead));
        Assert.Equal(0, await reader.ReadAsyncCore(charBuffer)); // End of stream
    }

    [Fact]
    public static async Task Should_Handle_Invalid_Utf8_At_Buffer_End_Async()
    {
        // Valid UTF-8 for "Test", followed by start of multi-byte char, then invalid byte
        // "€" is E2 82 AC. We'll use E2 82 FF (invalid continuation)
        byte[] invalidBytes = "Test"u8
            .ToArray()
            .Concat(new byte[] { 0xE2, 0x82, 0xFF }) // Invalid sequence starting like '€'
            .Concat("Data"u8.ToArray())
            .ToArray();

        var stream = new MemoryStream(invalidBytes);
        // Buffer size forces the invalid byte FF to be read in the next chunk
        var options = new CsvIOOptions { BufferSize = 6 }; // Reads "Test" + E2 82
        await using var reader = new Utf8StreamReader(stream, MemoryPool<char>.Shared, options);

        var charBuffer = new char[20]; // Ample space
        var sb = new StringBuilder();
        int charsRead;

        do
        {
            charsRead = await reader.ReadAsyncCore(charBuffer);
            sb.Append(charBuffer, 0, charsRead);
        } while (charsRead > 0);

        // Expect "Test" + Replacement Character (U+FFFD) + Replacement Character (U+FFFD) + "Data"
        // The invalid sequence E2 82 FF should result in two replacement chars because Utf8.ToUtf16 sees E2 82 first (NeedMoreData),
        // then reads FF, which is invalid on its own after the partial sequence.
        string expected = "Test\uFFFD\uFFFDData";
        Assert.Equal(expected, sb.ToString());
        Assert.Equal(0, await reader.ReadAsyncCore(charBuffer)); // End of stream
    }

    [Fact]
    public static async Task Should_Read_Empty_Stream_Async()
    {
        await using var reader = CreateReader("");
        var charBuffer = new char[10];
        int charsRead = await reader.ReadAsyncCore(charBuffer);
        Assert.Equal(0, charsRead);

        // Ensure reading again still returns 0
        charsRead = await reader.ReadAsyncCore(charBuffer);
        Assert.Equal(0, charsRead);
    }

    [Fact]
    public static async Task Should_Read_Large_Input_Async()
    {
        string content = string.Join(",", Enumerable.Range(0, 10000).Select(i => $"Value{i}"));
        await using var reader = CreateReader(content); // Relatively small buffer
        var sb = new StringBuilder();
        var charBuffer = new char[2048]; // Read in chunks
        int charsRead;

        do
        {
            charsRead = await reader.ReadAsyncCore(charBuffer);
            sb.Append(charBuffer, 0, charsRead);
        } while (charsRead > 0);

        Assert.Equal(content, sb.ToString());
        Assert.Equal(0, await reader.ReadAsyncCore(charBuffer)); // End of stream
    }

    [Fact]
    public static async Task Should_Reset_And_Read_Again_Async()
    {
        const string content = "First read.\nSecond read after reset.";
        await using var reader = CreateReader(content);
        var charBuffer = new char[content.Length * 2];
        int totalCharsRead1 = 0;
        int charsRead;

        // First read
        do
        {
            charsRead = await reader.ReadAsyncCore(charBuffer.AsMemory(totalCharsRead1));
            totalCharsRead1 += charsRead;
        } while (charsRead > 0);

        Assert.Equal(content.Length, totalCharsRead1);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead1));
        Assert.Equal(0, await reader.ReadAsyncCore(charBuffer)); // End of stream

        // Reset
        Assert.True(reader.TryReset());

        // Second read
        int totalCharsRead2 = 0;
        do
        {
            charsRead = await reader.ReadAsyncCore(charBuffer.AsMemory(totalCharsRead2));
            totalCharsRead2 += charsRead;
        } while (charsRead > 0);

        Assert.Equal(content.Length, totalCharsRead2);
        Assert.Equal(content, new string(charBuffer, 0, totalCharsRead2));
        Assert.Equal(0, await reader.ReadAsyncCore(charBuffer)); // End of stream again
    }
}

file static class Extensions
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReadCore")]
    public static extern int ReadCore(this Utf8StreamReader reader, Memory<char> buffer);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReadAsyncCore")]
    public static ValueTask<int> ReadAsyncCore(this Utf8StreamReader reader, Memory<char> buffer)
    {
        return ReadAsyncCoreImpl(reader, buffer, TestContext.Current.CancellationToken);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ReadAsyncCore")]
    private static extern ValueTask<int> ReadAsyncCoreImpl(
        Utf8StreamReader reader,
        Memory<char> buffer,
        CancellationToken cancellationToken);
}
