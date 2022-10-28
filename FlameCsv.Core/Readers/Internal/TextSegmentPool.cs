using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.Readers.Internal;

internal struct TextSegmentPool
{
    private TextSegment? _a;
    private TextSegment? _b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool TryPop([NotNullWhen(true)] out TextSegment? textSegment)
    {
        if (_a is not null)
        {
            textSegment = _a;
        }
        else if (_b is not null)
        {
            textSegment = _b;
        }
        else
        {
            textSegment = null;
            return false;
        }

        return true;
    }

    public void Push(TextSegment textSegment)
    {
        Debug.Assert(textSegment._array is null, "Segment rented memory should have been returned");

        if (_a is null)
        {
            _a = textSegment;
        }
        else if (_b is null)
        {
            _b = textSegment;
        }
    }
}
