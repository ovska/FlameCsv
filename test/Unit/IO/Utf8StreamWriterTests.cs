using System.Buffers;
using System.Text;
using FlameCsv.IO;
using FlameCsv.IO.Internal;

namespace FlameCsv.Tests.IO;

public class Utf8StreamWriterTests
{
    [Fact]
    public void Write_BasicAscii_EncodesCorrectly()
    {
        // Arrange
        using var ms = new MemoryStream();
        var options = new CsvIOOptions { BufferSize = 1024, LeaveOpen = true };
        var writer = new Utf8StreamWriter(ms, MemoryPool<char>.Shared, options);
        const string testData = "Hello, World!";

        // Act
        var span = writer.GetSpan(testData.Length);
        testData.AsSpan().CopyTo(span);
        writer.Advance(testData.Length);
        writer.Flush();

        // Assert
        var result = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(testData, result);

        writer.Complete(null);
    }

    [Fact]
    public async Task WriteAsync_BasicAscii_EncodesCorrectly()
    {
        // Arrange
        using var ms = new MemoryStream();
        var options = new CsvIOOptions { BufferSize = 1024, LeaveOpen = true };
        var writer = new Utf8StreamWriter(ms, MemoryPool<char>.Shared, options);
        const string testData = "Hello, World!";

        // Act
        var span = writer.GetSpan(testData.Length);
        testData.AsSpan().CopyTo(span);
        writer.Advance(testData.Length);
        await writer.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(testData, result);

        await writer.CompleteAsync(null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Write_MultiByteChars_EncodesCorrectly()
    {
        // Arrange
        using var ms = new MemoryStream();
        var options = new CsvIOOptions { BufferSize = 1024, LeaveOpen = true };
        var writer = new Utf8StreamWriter(ms, MemoryPool<char>.Shared, options);
        // Mix of 1, 2, 3, and 4-byte UTF-8 characters
        const string testData = "Hello, 世界! 😊 ñáéíóú";

        // Act
        var span = writer.GetSpan(testData.Length);
        testData.AsSpan().CopyTo(span);
        writer.Advance(testData.Length);
        writer.Flush();

        // Assert
        var result = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(testData, result);

        writer.Complete(null);
    }

    [Fact]
    public void Write_LargeData_HandlesMultipleTranscodes()
    {
        // Arrange
        using var ms = new MemoryStream();
        // Small buffer to force multiple transcode operations
        var options = new CsvIOOptions { BufferSize = 256, LeaveOpen = true };
        var writer = new Utf8StreamWriter(ms, MemoryPool<char>.Shared, options);
        // Data larger than the buffer
        var testData = new string('A', 800);

        // Act
        var span = writer.GetSpan(testData.Length);
        testData.AsSpan().CopyTo(span);
        writer.Advance(testData.Length);
        writer.Flush();

        // Assert
        var result = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(testData, result);

        writer.Complete(null);
    }

    [Fact]
    public async Task WriteAsync_EmojisAndSurrogatePairs_EncodesCorrectly()
    {
        // Arrange
        using var ms = new MemoryStream();
        var options = new CsvIOOptions { BufferSize = 64, LeaveOpen = true };
        var writer = new Utf8StreamWriter(ms, MemoryPool<char>.Shared, options);
        // Each emoji is a surrogate pair (2 UTF-16 chars, 4 UTF-8 bytes)
        const string testData = "😊😂🚀🎉👍";

        // Act
        var memory = writer.GetMemory(testData.Length);
        testData.AsSpan().CopyTo(memory.Span);
        writer.Advance(testData.Length);
        await writer.FlushAsync(TestContext.Current.CancellationToken);

        // Assert
        var result = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(testData, result);

        await writer.CompleteAsync(null, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Write_ExactBufferSize_HandlesCorrectly()
    {
        // Arrange
        const int bufferSize = 64;
        using var ms = new MemoryStream();
        var options = new CsvIOOptions { BufferSize = bufferSize, LeaveOpen = true };
        var writer = new Utf8StreamWriter(ms, MemoryPool<char>.Shared, options);

        // Create content exactly matching buffer size
        var testData = new string('A', bufferSize);

        // Act
        var span = writer.GetSpan(testData.Length);
        testData.AsSpan().CopyTo(span);
        writer.Advance(testData.Length);
        writer.Flush();

        // Assert
        var result = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(testData, result);

        writer.Complete(null);
    }

    [Fact]
    public void GetSpan_RequestLargerThanBuffer_ExpandsCapacity()
    {
        // Arrange
        using var ms = new MemoryStream();
        var options = new CsvIOOptions { BufferSize = 512, LeaveOpen = true };
        var writer = new Utf8StreamWriter(ms, MemoryPool<char>.Shared, options);

        // Act - Request a span larger than default buffer
        var span = writer.GetSpan(1500);

        // Assert
        Assert.True(span.Length >= 1500);

        writer.Complete(null);
    }

    [Fact]
    public void Advance_BeyondAvailable_ThrowsException()
    {
        // Arrange
        using var ms = new MemoryStream();
        var options = new CsvIOOptions { BufferSize = 512, LeaveOpen = true };
        var writer = new Utf8StreamWriter(ms, MemoryPool<char>.Shared, options);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.Advance(5000));

        // Clean up
    }

    [Fact]
    public async Task MultipleOperations_Success()
    {
        // Arrange
        using var ms = new MemoryStream();
        var options = new CsvIOOptions { BufferSize = 64, LeaveOpen = true };
        var writer = new Utf8StreamWriter(ms, MemoryPool<char>.Shared, options);

        // Act - Multiple operations sequence
        // Write 1
        const string data1 = "First batch";
        var memory = writer.GetMemory(data1.Length);
        data1.AsSpan().CopyTo(memory.Span);
        writer.Advance(data1.Length);
        await writer.FlushAsync(TestContext.Current.CancellationToken);

        // Write 2
        const string data2 = "Second with UTF-8: 日本語";
        memory = writer.GetMemory(data2.Length);
        data2.AsSpan().CopyTo(memory.Span);
        writer.Advance(data2.Length);
        await writer.FlushAsync(TestContext.Current.CancellationToken);

        // Write 3
        const string data3 = "Third with emoji: 🚀";
        var span = writer.GetSpan(data3.Length);
        data3.AsSpan().CopyTo(span);
        writer.Advance(data3.Length);
        // ReSharper disable once MethodHasAsyncOverload
        writer.Flush();

        // Assert
        var result = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(data1 + data2 + data3, result);

        await writer.CompleteAsync(null, TestContext.Current.CancellationToken);
    }

    // Helper for testing error conditions
    private class FailingStream : MemoryStream
    {
        public bool FailOnNextWrite { get; set; }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (FailOnNextWrite)
                throw new IOException("Simulated write failure");

            base.Write(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (FailOnNextWrite)
                throw new IOException("Simulated write failure");

            return base.WriteAsync(buffer, cancellationToken);
        }
    }

    [Fact]
    public async Task WriteAsync_StreamThrowsException_PropagatesException()
    {
        // Arrange
        var failingStream = new FailingStream();
        var options = new CsvIOOptions { BufferSize = 32, LeaveOpen = true };
        var writer = new Utf8StreamWriter(failingStream, MemoryPool<char>.Shared, options);

        const string testData = "Test data";
        var span = writer.GetSpan(testData.Length);
        testData.AsSpan().CopyTo(span);
        writer.Advance(testData.Length);

        // Make stream fail
        failingStream.FailOnNextWrite = true;

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(async () =>
            await writer.FlushAsync(TestContext.Current.CancellationToken));

        await writer.CompleteAsync(null, TestContext.Current.CancellationToken);
    }
}
