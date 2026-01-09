using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct RecordView(int start, int length)
{
    public int Start => start;
    public int Length => length;

    public override string ToString() => $"RecordView(Start={Start}, Length={Length})";

    public long GetEndPosition(RecordBuffer recordBuffer)
    {
        Check.NotNull(recordBuffer);
        return recordBuffer.GetEnd(Start + Length);
    }

    [Conditional("DEBUG")]
    public void AssertInvariants()
    {
        Check.True(
            Start >= 0 && Length > 0,
            $"RecordView must have positive start and length: Start={Start}, Length={Length}"
        );
    }
}
