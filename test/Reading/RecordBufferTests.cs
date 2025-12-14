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
        Assert.Equal(4, view.Length);
        Assert.Equal(9, buffer.GetLengthWithNewline(view)); // 7 + CRLF

        // first 8 (+ 0 at start) were read
        Assert.Equal(bufferSize - 9, buffer.GetUnreadBuffer(0, out int startIndex).Fields.Length);
        Assert.Equal(41, startIndex);

        Assert.False(buffer.TryPop(out _));

        // we read 8 fields, but only the first 4 had a record (EOL at 7+2)
        Assert.Equal(9, buffer.Reset());

        buffer.GetUnreadBuffer(0, out startIndex).Fields[0] = 41u | Field.IsCRLF;
        buffer.SetFieldsRead(1);
        Assert.True(buffer.TryPop(out view));
        Assert.Equal(0, view.Start);
        Assert.Equal(1, view.Length);
        Assert.Equal(43, buffer.GetLengthWithNewline(view)); // 41 + CRLF
    }

    [Fact]
    public void Should_Reset_Quotes()
    {
        using var rb = new RecordBuffer();

        FieldBuffer dst = rb.GetUnreadBuffer(0, out int start);
        Assert.Equal(0, start);

        ((ReadOnlySpan<uint>)[1, 2, 3, (4 | Field.IsEOL | Field.IsCRLF)]).CopyTo(dst.Fields);
        ((ReadOnlySpan<byte>)[0, 4, 0, 4]).CopyTo(dst.Quotes);

        int records = rb.SetFieldsRead(4);
        Assert.Equal(1, records);

        Assert.True(rb.TryPop(out var view));
        Assert.Equal(0, view.Start);
        Assert.Equal(4, view.Length);

        int dataRead = rb.Reset();
        Assert.Equal(4 + 2, dataRead); // trailing CRLF

        // all quotes should be cleared
        Assert.Equal(-1, rb._quotes.IndexOfAnyExcept((byte)0));
    }

    [Fact]
    public void Should_Set_Fields_Read()
    {
        using var rb = new RecordBuffer();

        FieldBuffer dst = rb.GetUnreadBuffer(0, out int start);

        // use a large prime so tail handling is tested
        const int count = 1051;

        for (int i = 0; i < count; i++)
        {
            uint value = (uint)((i + 1) * 3);

            if (i % 5 == 0)
            {
                value |= Field.IsEOL;
            }

            dst.Fields[i] = value;

            if (i % 3 == 0)
            {
                dst.Quotes[i] = (byte)2;
            }
            else if (i % 7 == 0)
            {
                dst.Quotes[i] = (byte)4;
            }
        }

        int records = rb.SetFieldsRead(count);

        string[] packed;

        if (System.Diagnostics.Debugger.IsAttached)
        {
            packed = rb
                ._bits.Take(count)
                .Select(x =>
                {
                    var s = ((int)x).ToString().PadLeft(5, ' ');
                    var e = (((int)(x >> 32)) & (int)Field.EndMask).ToString().PadLeft(5, ' ');
                    return $"{s},{e}";
                })
                .ToArray();
        }

        for (int i = 0; i < count; i++)
        {
            ulong v = rb._bits[i];

            int s = (int)rb._bits[i];
            int e = (int)(rb._bits[i] >> 32) & (int)Field.EndMask;

            Assert.Equal((i * 3 + (i == 0 ? 0 : 1)), s);
            Assert.Equal(((i + 1) * 3), e);

            if (i % 3 == 0)
            {
                Assert.Equal(0b10UL, v >> 62);
            }
            else if (i % 7 == 0)
            {
                Assert.Equal(0b11UL, v >> 62);
            }
            else
            {
                Assert.Equal(0UL, (v >> 62));
            }
        }
    }
}
