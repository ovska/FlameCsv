using FlameCsv.Reading.Internal;

namespace FlameCsv.Tests.Reading;

public class RecordBufferTests
{
    [Fact]
    public void Should_Read_Fields()
    {
        const int bufferSize = 256;

        using RecordBuffer buffer = new(bufferSize);
        ((ReadOnlySpan<uint>)[RecordBuffer.FirstSentinel, 1u, 3u, 5u, 7u | Field.IsCRLF, 10u, 20u, 30u, 40u]).CopyTo(
            buffer._fields
        );

        uint[] array = buffer._fields;

        buffer.SetFieldsRead(8);

        Assert.Equal(8, buffer.UnreadFields);
        Assert.Equal(41, buffer.BufferedDataLength); // +1 for trailing comma
        Assert.Equal(1, buffer.UnreadRecords);
        Assert.Equal(9, buffer.BufferedRecordLength); // 7+2 (crlf)

        Assert.True(buffer.TryPop(out var view));
        Assert.Equal(0, view.Start);
        Assert.Equal(4, view.Length);
        Assert.Equal(9, buffer.GetLengthWithNewline(view)); // 7 + CRLF

        // first 8 (+ 0 at start) were read
        Assert.Equal(bufferSize - 9, buffer.GetUnreadBuffer(0, out int startIndex).Length);
        Assert.Equal(41, startIndex);

        Assert.False(buffer.TryPop(out _));

        // we read 8 fields, but only the first 4 had a record (EOL at 7+2)
        Assert.Equal(9, buffer.Reset());

        buffer.GetUnreadBuffer(0, out startIndex)[0] = 41u | Field.IsCRLF;
        buffer.SetFieldsRead(1);
        Assert.True(buffer.TryPop(out view));
        Assert.Equal(0, view.Start);
        Assert.Equal(1, view.Length);
        Assert.Equal(43, buffer.GetLengthWithNewline(view)); // 41 + CRLF
    }

    [Fact]
    public void Should_Set_Fields_Read()
    {
        using var rb = new RecordBuffer();

        Span<uint> dst = rb.GetUnreadBuffer(0, out int _);

        // use a large prime so tail handling is tested
        const int count = 1051;

        for (int i = 0; i < count; i++)
        {
            uint value = (uint)((i + 1) * 3);

            if (i % 5 == 0)
            {
                value |= Field.IsEOL;
            }

            dst[i] = value;

            if (i % 3 == 0)
            {
                dst[i] |= Field.IsQuotedMask;
            }
            else if (i % 7 == 0)
            {
                dst[i] |= Field.NeedsUnescapingMask;
            }
        }

        int records = rb.SetFieldsRead(count);

        for (int i = 0; i < (count - 1); i++)
        {
            uint previous = rb._fields[i];
            uint current = rb._fields[i + 1];

            int start = Field.NextStart(previous);
            int end = Field.End(current);

            Assert.Equal((i * 3 + (i == 0 ? 0 : 1)), start);
            Assert.Equal(((i + 1) * 3), end);

            if (i % 3 == 0)
            {
                Assert.NotEqual(0u, current & Field.IsQuotedMask);
            }
            else if (i % 7 == 0)
            {
                Assert.NotEqual(0u, current & Field.NeedsUnescapingMask);
            }
            else
            {
                Assert.Equal(0u, current & (Field.IsQuotedMask | Field.NeedsUnescapingMask));
            }
        }
    }
}
