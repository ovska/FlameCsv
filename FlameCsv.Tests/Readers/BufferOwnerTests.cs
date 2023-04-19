using System.Buffers;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Tests.Readers;

public static class BufferOwnerTests
{
    [Fact]
    public static void Should_Use_Same_Array_Ref()
    {
        char[]? arr = null;

        try
        {
            var vbo = new BufferOwner<char>(ref arr, ArrayPool<char>.Shared);
            var span = vbo.GetSpan(5);
            Assert.Equal(5, span.Length);
            Assert.NotNull(arr);
            Assert.True(span.Overlaps(arr, out int offset));
            Assert.Equal(0, offset);
        }
        finally
        {
            ArrayPool<char>.Shared.EnsureReturned(ref arr);
        }
    }
}
