using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Reading;

public class RecordBufferTests
{
    [Fact]
    public void Should_Read_Fields()
    {
        const int bufferSize = 256;

        using RecordBuffer buffer = new(bufferSize);
        ((ReadOnlySpan<uint>)[0, 1u, 3u, 5u, 7u | Field.IsCRLF, 10u, 20u, 30u, 40u]).CopyTo(buffer._fields);

        uint[] array = buffer._fields;

        buffer.SetFieldsRead(8);

        Assert.Equal(8, buffer.UnreadFields);
        Assert.Equal(41, buffer.BufferedDataLength); // +1 for trailing comma
        Assert.Equal(1, buffer.UnreadRecords);
        Assert.Equal(9, buffer.BufferedRecordLength); // 7+2 (crlf)

        Assert.True(buffer.TryPop(out var view));
        Assert.Equal(0, view.Start);
        Assert.Equal(5, view.Count);
        Assert.Equal(9, view.GetLengthWithNewline(buffer)); // 7 + CRLF

        // first 8 (+ 0 at start) were read
        Assert.Equal(bufferSize - 9, buffer.GetUnreadBuffer(0, out int startIndex).Fields.Length);
        Assert.Equal(41, startIndex);

        Assert.False(buffer.TryPop(out _));

        // we read 8 fields, but only the first 4 had a record (EOL at 7+2)
        Assert.Equal(9, buffer.Reset());
        Assert.Equal([0u, 1u, 11u, 21u, 31u], buffer._fields.AsSpan(0, 5));

        buffer.GetUnreadBuffer(0, out startIndex).Fields[0] = 41u | Field.IsCRLF;
        buffer.SetFieldsRead(1);
        Assert.True(buffer.TryPop(out view));
        Assert.Equal(0, view.Start);
        Assert.Equal(6, view.Count);
        Assert.Equal(43, view.GetLengthWithNewline(buffer)); // 41 + CRLF
    }
}
