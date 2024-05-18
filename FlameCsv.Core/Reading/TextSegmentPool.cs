using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

internal struct TextSegmentPool
{
    private TextSegment? _a;
    private TextSegment? _b;
    private TextSegment? _c;

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
        else if (_c is not null)
        {
            textSegment = _c;
            _c = null;
        }
        else
        {
            textSegment = null;
        }

        return textSegment is not null;
    }

    [SuppressMessage("Style", "IDE0074:Use compound assignment", Justification = "Readability")]
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
        else if (_c is null)
        {
            _c = textSegment;
        }
    }
}
