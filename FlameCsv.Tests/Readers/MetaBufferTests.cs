using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Readers;

public class MetaBufferTests
{
    [Fact]
    public void Should_Return_Buffer()
    {
        MetaBuffer buffer = new();
        var array = new Meta[4];
        array[0] = Meta.StartOfData;
        buffer.UnsafeGetArrayRef() = array;

        Assert.Equal(3, buffer.GetUnreadBuffer(out int startIndex).Length);
        Assert.Equal(0, startIndex);

        buffer.Dispose();
        Assert.Empty(buffer.UnsafeGetArrayRef());
    }

    [Fact]
    public void Should_Read_Fields()
    {
        MetaBuffer buffer = new();
        Meta[] array =
        [
            Meta.StartOfData,
            Meta.Plain(1),
            Meta.Plain(3),
            Meta.Plain(5),
            Meta.Plain(7, isEOL: true, 2),
            Meta.Plain(10),
            Meta.Plain(20),
            Meta.Plain(30),
            Meta.Plain(40),
            default,
            default,
            default,
        ];
        buffer.UnsafeGetArrayRef() = array;

        int consumed = buffer.SetFieldsRead(8);
        Assert.Equal(41, consumed);

        Assert.True(buffer.TryPop(out var fields));
        Assert.Equal(array[..5].AsSpan(), fields.AsSpan());

        // first 8 (+ startofdata) were read
        Assert.Equal(3, buffer.GetUnreadBuffer(out int startIndex).Length);
        Assert.Equal(41, startIndex);

        Assert.False(buffer.TryPop(out _));

        // we read 8 fields, but only the first 4 had a record (EOL at 7+2)
        Assert.Equal(9, buffer.Reset());
        Assert.Equal(
            [Meta.StartOfData, Meta.Plain(1), Meta.Plain(11), Meta.Plain(21), Meta.Plain(31)],
            buffer.UnsafeGetArrayRef().AsSpan(0, 5));

        buffer.GetUnreadBuffer(out startIndex)[0] = Meta.Plain(41, isEOL: true, 2);
        consumed = buffer.SetFieldsRead(1);
        Assert.Equal(43, consumed); // 41+2
        Assert.True(buffer.TryPop(out fields));
        Assert.Equal(array[..6].AsSpan(), fields.AsSpan());
    }
}
