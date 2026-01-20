using System.Buffers;
using FlameCsv.IO;

namespace FlameCsv.Tests.Extensions;

public class MemoryPoolExtensionTests
{
    [Fact]
    public void EnsureCapacity_NegativeMinimumLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = null;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => pool.EnsureCapacity(ref memoryOwner, -1));
    }

    [Fact]
    public void EnsureCapacity_ZeroMinimumLength_ReturnsEmptyMemory()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = null;

        // Act
        var result = pool.EnsureCapacity(ref memoryOwner, 0);

        // Assert
        Assert.True(result.IsEmpty);
        Assert.Null(memoryOwner);
    }

    [Fact]
    public void EnsureCapacity_NullMemoryOwner_CreatesNewMemoryOwner()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = null;
        const int minimumLength = 100;

        // Act
        var result = pool.EnsureCapacity(ref memoryOwner, minimumLength);

        // Assert
        Assert.NotNull(memoryOwner);
        Assert.True(result.Length >= minimumLength);
        Assert.Equal(memoryOwner.Memory.Length, result.Length);

        // Cleanup
        memoryOwner?.Dispose();
    }

    [Fact]
    public void EnsureCapacity_SufficientCapacity_ReturnsExistingMemory()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = pool.Rent<char>(200);
        var originalLength = memoryOwner.Memory.Length;
        var originalPin = memoryOwner.Memory.Pin();
        const int minimumLength = 100;

        try
        {
            // Act
            var result = pool.EnsureCapacity(ref memoryOwner, minimumLength);

            // Assert
            Assert.Equal(originalLength, result.Length);
            Assert.True(result.Length >= minimumLength);

            // Verify it's the same memory by comparing pinned addresses
            var resultPin = result.Pin();
            try
            {
                unsafe
                {
                    Assert.True(originalPin.Pointer == resultPin.Pointer);
                }
            }
            finally
            {
                resultPin.Dispose();
            }
        }
        finally
        {
            originalPin.Dispose();
            memoryOwner?.Dispose();
        }
    }

    [Fact]
    public void EnsureCapacity_InsufficientCapacity_WithoutCopy_CreatesNewMemory()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = pool.Rent<char>(50);
        var originalLength = memoryOwner.Memory.Length;
        var originalPin = memoryOwner.Memory.Pin();
        const int minimumLength = 200;

        try
        {
            // Act
            var result = pool.EnsureCapacity(ref memoryOwner, minimumLength, copyOnResize: false);

            // Assert
            Assert.NotEqual(originalLength, result.Length);
            Assert.True(result.Length >= minimumLength);
            Assert.NotNull(memoryOwner);
            Assert.Equal(memoryOwner.Memory.Length, result.Length);

            // Verify it's different memory by comparing pinned addresses
            var resultPin = result.Pin();
            try
            {
                unsafe
                {
                    Assert.True(originalPin.Pointer != resultPin.Pointer);
                }
            }
            finally
            {
                resultPin.Dispose();
            }
        }
        finally
        {
            originalPin.Dispose();
            memoryOwner?.Dispose();
        }
    }

    [Fact]
    public void EnsureCapacity_InsufficientCapacity_WithCopy_CopiesExistingData()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = pool.Rent<char>(50);
        var originalLength = memoryOwner.Memory.Length;

        // Fill original memory with test data
        var testData = "Hello, World!".AsSpan();
        testData.CopyTo(memoryOwner.Memory.Span);

        const int minimumLength = 200;

        // Act
        var result = pool.EnsureCapacity(ref memoryOwner, minimumLength, copyOnResize: true);

        // Assert
        Assert.NotEqual(originalLength, result.Length);
        Assert.True(result.Length >= minimumLength);

        // Verify data was copied
        var copiedData = result.Span.Slice(0, testData.Length);
        Assert.True(copiedData.SequenceEqual(testData));

        // Cleanup
        memoryOwner?.Dispose();
    }

    [Fact]
    public void EnsureCapacity_EmptyMemoryWithExistingOwner_CreatesNewMemory()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = new EmptyMemoryOwner<char>();
        const int minimumLength = 100;

        // Act
        var result = pool.EnsureCapacity(ref memoryOwner, minimumLength);

        // Assert
        Assert.True(result.Length >= minimumLength);
        Assert.NotNull(memoryOwner);
        Assert.IsNotType<EmptyMemoryOwner<char>>(memoryOwner);

        // Cleanup
        memoryOwner?.Dispose();
    }

    [Fact]
    public void EnsureCapacity_ExactMinimumLength_ReturnsExistingMemory()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = pool.Rent<char>(100);
        var originalLength = memoryOwner.Memory.Length;
        int minimumLength = originalLength; // Exact match
        var originalPin = memoryOwner.Memory.Pin();
        var originalMemory = memoryOwner.Memory;

        try
        {
            // Act
            var result = pool.EnsureCapacity(ref memoryOwner, minimumLength);

            // Assert
            Assert.Equal(originalLength, result.Length);

            // Verify it's the same memory by comparing pinned addresses
            var resultPin = result.Pin();
            try
            {
                unsafe
                {
                    Assert.True(originalPin.Pointer == resultPin.Pointer);
                }
            }
            finally
            {
                resultPin.Dispose();
            }
        }
        finally
        {
            originalPin.Dispose();
            memoryOwner?.Dispose();
        }
    }

    [Fact]
    public void EnsureCapacity_MultipleResizes_WorksCorrectly()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = null;

        try
        {
            // First resize
            var result1 = pool.EnsureCapacity(ref memoryOwner, 50);
            Assert.True(result1.Length >= 50);

            // Second resize (larger)
            var result2 = pool.EnsureCapacity(ref memoryOwner, 200);
            Assert.True(result2.Length >= 200);

            // Third resize (smaller - should return existing)
            var originalPin = result2.Pin();
            try
            {
                var result3 = pool.EnsureCapacity(ref memoryOwner, 100);
                Assert.Equal(result2.Length, result3.Length);

                // Verify it's the same memory
                var result3Pin = result3.Pin();
                try
                {
                    unsafe
                    {
                        Assert.True(originalPin.Pointer == result3Pin.Pointer);
                    }
                }
                finally
                {
                    result3Pin.Dispose();
                }
            }
            finally
            {
                originalPin.Dispose();
            }
        }
        finally
        {
            memoryOwner?.Dispose();
        }
    }

    [Fact]
    public void EnsureCapacity_WithZeroCapacityOwner_CreatesNewMemory()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = new ZeroCapacityMemoryOwner<char>();
        const int minimumLength = 100;

        // Act
        var result = pool.EnsureCapacity(ref memoryOwner, minimumLength);

        // Assert
        Assert.True(result.Length >= minimumLength);
        Assert.NotNull(memoryOwner);
        Assert.IsNotType<ZeroCapacityMemoryOwner<char>>(memoryOwner);

        // Cleanup
        memoryOwner?.Dispose();
    }

    [Fact]
    public void EnsureCapacity_CopyOnResize_PreservesDataExactly()
    {
        // Arrange
        var pool = DefaultBufferPool.Instance;
        IMemoryOwner<char>? memoryOwner = pool.Rent<char>(20);

        // Fill with specific pattern
        var span = memoryOwner.Memory.Span;
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = (char)('A' + (i % 26));
        }
        var originalData = span.ToArray();

        const int minimumLength = 100;

        // Act
        var result = pool.EnsureCapacity(ref memoryOwner, minimumLength, copyOnResize: true);

        // Assert
        Assert.True(result.Length >= minimumLength);

        // Verify all original data was preserved
        var resultSpan = result.Span;
        for (int i = 0; i < originalData.Length; i++)
        {
            Assert.Equal(originalData[i], resultSpan[i]);
        }

        // Cleanup
        memoryOwner?.Dispose();
    }

    // Helper classes for testing
    private class CustomMemoryPool<T> : MemoryPool<T>
    {
        private readonly int _maxBufferSize;

        public CustomMemoryPool(int maxBufferSize)
        {
            _maxBufferSize = maxBufferSize;
        }

        public override int MaxBufferSize => _maxBufferSize;

        public override IMemoryOwner<T> Rent(int minBufferSize = -1)
        {
            return MemoryPool<T>.Shared.Rent(minBufferSize);
        }

        protected override void Dispose(bool disposing)
        {
            // No resources to dispose
        }
    }

    private class EmptyMemoryOwner<T> : IMemoryOwner<T>
    {
        public Memory<T> Memory => Memory<T>.Empty;

        public void Dispose()
        {
            // No resources to dispose
        }
    }

    private class ZeroCapacityMemoryOwner<T> : IMemoryOwner<T>
    {
        public Memory<T> Memory => Memory<T>.Empty;

        public void Dispose()
        {
            // No resources to dispose
        }
    }
}
