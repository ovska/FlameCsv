using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Reading;

public class MetaBufferTests
{
    [Fact]
    public void Should_Read_Fields()
    {
        // Assert.Skip("Should not re-parse leftover fields");

        // this will leak some arrays from pool but that's fine for a test
        RecordBuffer buffer = new();
        uint[] array =
        [
            Field.StartOrEnd,
            1u,
            3u,
            5u,
            7u | (uint)FieldFlag.CRLF,
            10u,
            20u,
            30u,
            40u,
            default,
            default,
            default,
        ];

        buffer.GetFieldArrayRef() = array;

        buffer.SetFieldsRead(8);

        Assert.True(buffer.TryPop(out var view));
        Assert.Equal(array[..5].AsSpan(), view.Fields);

        // first 8 (+ startofdata) were read
        // TODO! don't re-parse
        Assert.Equal(3, buffer.GetUnreadBuffer(0, out int startIndex).Fields.Length);
        Assert.Equal(41, startIndex);

        Assert.False(buffer.TryPop(out _));

        // we read 8 fields, but only the first 4 had a record (EOL at 7+2)
        Assert.Equal(9, buffer.Reset());
        Assert.Equal([Field.StartOrEnd, 1u, 11u, 21u, 31u], buffer.GetFieldArrayRef().AsSpan(0, 5));

        buffer.GetUnreadBuffer(0, out startIndex).Fields[0] = 41u | (uint)FieldFlag.CRLF;
        buffer.SetFieldsRead(1);
        Assert.True(buffer.TryPop(out view));
        Assert.Equal(array[..6].AsSpan(), view.Fields);
    }
}
