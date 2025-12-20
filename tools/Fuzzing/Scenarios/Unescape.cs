using System.Runtime.InteropServices;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Fuzzing.Scenarios;

public class Unescape : IScenario
{
    public static bool SupportsUtf16 => true;

    public static void Run(ReadOnlyMemory<byte> data, PoisonPagePlacement placement) => RunCore(data.Span, placement);

    public static void Run(ReadOnlyMemory<char> data, PoisonPagePlacement placement) => RunCore(data.Span, placement);

    private static void RunCore<T>(ReadOnlySpan<T> data, PoisonPagePlacement placement)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (data.Length < 2)
            return;

        try
        {
            T quote = T.CreateTruncating('"');

            if (data.IndexOf(quote) == -1)
                return;

            using var memory = PooledBoundedMemory<T>.Rent(data.Length, placement);
            Span<T> destination = memory.Span.Slice(0, data.Length);

            if (typeof(T) == typeof(char))
            {
                Field.Unescape(
                    ushort.CreateTruncating(quote),
                    MemoryMarshal.Cast<T, ushort>(destination),
                    MemoryMarshal.Cast<T, ushort>(data)
                );
            }
            else
            {
                Field.Unescape(quote, destination, data);
            }
        }
        catch (CsvFormatException) { }
    }
}
