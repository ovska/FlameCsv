using System.Buffers;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Tests.Readers;

public static class BufferOwnerTests
{
    [Fact]
    public static void Should_Throw_If_Disposed()
    {
        using var buffer = new BufferOwner<char>();
        buffer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => buffer.GetSpan(1));
    }

    [Fact]
    public static void Should_Return_Span()
    {
        var pool = new TestPool<char>();
        using var buffer = new BufferOwner<char>(SecurityLevel.Strict, pool);
        Span<char> span = buffer.GetSpan(123);
        Assert.Equal(123, span.Length);
        buffer.Dispose();
        Assert.Single(pool.Returned);
        Assert.True(pool.Returned[0].clearArray);
    }

    [Fact]
    public static void Should_Return_Array_To_Pool()
    {
        var pool = new TestPool<char>();
        using var buffer = new BufferOwner<char>(SecurityLevel.Strict, pool);
        var span = buffer.GetSpan(123);
        Assert.Equal(123, span.Length);
        buffer.Dispose();
        Assert.Single(pool.Returned);
        Assert.True(pool.Returned[0].array.Length >= 123);
        Assert.True(pool.Returned[0].clearArray);
    }

    [Fact]
    public static void Should_Not_Clear_Array()
    {
        var pool = new TestPool<char>();
        using var buffer = new BufferOwner<char>(SecurityLevel.NoBufferClearing, pool);
        _ = buffer.GetSpan(123);
        buffer.Dispose();
        Assert.Single(pool.Returned);
        Assert.False(pool.Returned[0].clearArray);
    }

    private sealed class TestPool<T> : ArrayPool<T>
    {
        public readonly List<(T[] array, bool clearArray)> Returned = new();

        public override T[] Rent(int minimumLength)
        {
            return new T[(int)Math.Pow(2, (int)Math.Log(minimumLength - 1, 2) + 1)];
        }

        public override void Return(T[] array, bool clearArray = false)
        {
            if (array.Length > 0) Returned.Add((array, clearArray));
        }
    }
}
