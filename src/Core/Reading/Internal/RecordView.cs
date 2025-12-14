using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct RecordView(int start, int length)
{
    public int Start => start;
    public int Length => length;
}
