using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

[SkipLocalsInit]
[InlineArray(Length)]
internal struct EnumeratorStack
{
    public const int Length = 256;
    public byte elem0;
}
