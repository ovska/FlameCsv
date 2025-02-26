using System.Runtime.CompilerServices;

namespace FlameCsv.Utilities;

[InlineArray(MaxLength)]
internal struct StringScratch
{
    public string elem0;
    public const int MaxLength = 16;
}

internal static class StringScratchExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<string> AsSpan(ref this StringScratch scratch, int length)
    {
        return ((Span<string>)scratch).Slice(0, length);
    }
}
