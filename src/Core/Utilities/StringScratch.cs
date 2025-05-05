using System.Runtime.CompilerServices;

namespace FlameCsv.Utilities;

[InlineArray(MaxLength)]
internal struct StringScratch
{
    public string elem0;
    public const int MaxLength = 16;
}
