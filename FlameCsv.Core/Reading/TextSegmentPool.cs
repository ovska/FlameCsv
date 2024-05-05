using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

// most likely only 2 segments are needed at a time
internal struct TextSegmentPool
{
    private TextSegment? _a;
    private TextSegment? _b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop([NotNullWhen(true)] out TextSegment? textSegment)
    {
        if (_a is not null)
        {
            textSegment = _a;
            _a = null;
        }
        else if (_b is not null)
        {
            textSegment = _b;
            _b = null;
        }
        else
        {
            textSegment = null;
        }

        return textSegment is not null;
    }

    public void Push(TextSegment textSegment)
    {
        Debug.Assert(textSegment._array is null, "Segment rented memory should have been returned");

        if (_a is null)
        {
            _a = textSegment;
        }
        else
        {
            _b ??= textSegment;
        }
    }
}
