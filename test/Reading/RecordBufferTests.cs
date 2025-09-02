using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Reading;

public class RecordBufferTests
{
    [Fact]
    public void Should_Read_Fields()
    {
        using RecordBuffer buffer = new();
        uint[] array =
        [
            /**/
            0,
            1u,
            3u,
            5u,
            7u | Field.IsCRLF,
            10u,
            20u,
            30u,
            40u,
            default,
            default,
            default,
        ];

        uint[] poolArray = buffer.GetFieldArrayRef();
        buffer.GetFieldArrayRef() = array;

        buffer.SetFieldsRead(8);

        Assert.True(buffer.TryPop(out var view));
        Assert.Equal(array[..5].AsSpan(), view.Fields);

        // first 8 (+ startofdata) were read
        Assert.Equal(3, buffer.GetUnreadBuffer(0, out int startIndex).Fields.Length);
        Assert.Equal(41, startIndex);

        Assert.False(buffer.TryPop(out _));

        // we read 8 fields, but only the first 4 had a record (EOL at 7+2)
        Assert.Equal(9, buffer.Reset());
        Assert.Equal([0u, 1u, 11u, 21u, 31u], buffer.GetFieldArrayRef().AsSpan(0, 5));

        buffer.GetUnreadBuffer(0, out startIndex).Fields[0] = 41u | Field.IsCRLF;
        buffer.SetFieldsRead(1);
        Assert.True(buffer.TryPop(out view));
        Assert.Equal(array[..6].AsSpan(), view.Fields);

        buffer.GetFieldArrayRef() = poolArray;
    }
}
