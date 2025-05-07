using System.Runtime.CompilerServices;

namespace FlameCsv.Utilities;

/// <summary>
/// 16 string item inline array.
/// </summary>
[InlineArray(MaxLength)]
internal struct StringScratch
{
    public string elem0;
    public const int MaxLength = 16;
}
