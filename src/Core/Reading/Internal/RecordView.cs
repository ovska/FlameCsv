using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct RecordView(int start, int length)
{
    public int Start => start;
    public int Length => length;

    public long GetEndPosition(RecordBuffer recordBuffer)
    {
        Check.NotNull(recordBuffer);
        return recordBuffer.GetEnd(Start + Length);
    }
    
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public override string ToString() => $"RecordView(Start={Start}, Length={Length})";
}
