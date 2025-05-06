using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Reading.Unescaping;

namespace FlameCsv.Fuzzing.Scenarios;

// ReSharper disable once ClassNeverInstantiated.Global
public class Unescape : IScenario
{
    public static bool SupportsUtf16 => true;

    public static void Run(ReadOnlyMemory<byte> data, PoisonPagePlacement placement) => RunCore(data.Span, placement);

    public static void Run(ReadOnlyMemory<char> data, PoisonPagePlacement placement) => RunCore(data.Span, placement);

    private static void RunCore<T>(ReadOnlySpan<T> data, PoisonPagePlacement placement)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (data.Length < 2) return;

        try
        {
            T quote = T.CreateTruncating('"');
            uint count = (uint)data.Count(quote);

            if (count == 0) return;

            using var memory = PooledBoundedMemory<T>.Rent(data.Length, placement);
            Span<T> destination = memory.Span.Slice(0, data.Length);

            if (typeof(T) == typeof(char))
            {
                RFC4180Mode<ushort>.Unescape(
                    ushort.CreateTruncating(quote),
                    MemoryMarshal.Cast<T, ushort>(destination),
                    MemoryMarshal.Cast<T, ushort>(data),
                    count);
            }
            else
            {
                RFC4180Mode<T>.Unescape(quote, destination, data, count);
            }

            // ensure equality
            int unescapedLength = IndexOfRFC4180Unescaper<T>.UnescapedLength(data.Length, count);
            var unescaper = new IndexOfRFC4180Unescaper<T>(quote, count);
            using var assertionMemory = PooledBoundedMemory<T>.Rent(unescapedLength, placement);
            Span<T> assertion = assertionMemory.Span.Slice(0, unescapedLength);
            IndexOfUnescaper.Field(data, unescaper, assertion);

            if (!assertion.SequenceEqual(destination.Slice(0, unescapedLength)))
            {
                throw new Exception(
                    $"Unescaped data does not match. Expected: {assertion.AsPrintableString()} Actual: {destination.Slice(0, unescapedLength).AsPrintableString()}");
            }
        }
        catch (CsvFormatException)
        {
        }
    }
}
