using System.Runtime.CompilerServices;

namespace FlameCsv.Benchmark;

[SimpleJob]
public class UnescapeBench
{
    private readonly char[] _buffer = new char[1024];
    private (string, int)[] testData = null!;

    [Benchmark(Baseline = true)]
    public void IndexOf()
    {
        foreach (var (str, count) in testData)
            _ = UnescapeUncommon2(str.AsSpan(), _buffer, '"', count);
    }

    [Benchmark]
    public void Foreach()
    {
        foreach (var (str, count) in testData)
            _ = UnescapeUncommon(str.AsSpan(), _buffer, '"', count);
    }

    [GlobalSetup]
    public void Setup()
    {
        string[] data =
        {
            "Sho\"\"rt",
            "Semi \"\"long\"\" sentence",
            "Business Data Collection - BDC\"\",\"\" Industry by employment variable",
            "James \"\"007\"\" Bond",
            "Some long sentence with a \"\"quoted\"\" word in int.",
        };
        testData = data.Select(s => (s, s.Count(c => c == '"'))).ToArray();
    }

    public static ReadOnlySpan<T> UnescapeUncommon2<T>(
        ReadOnlySpan<T> source,
        Span<T> buffer,
        T quote,
        int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        int written = 0;
        int index = 0;
        ReadOnlySpan<T> needle = stackalloc T[] { quote, quote };

        while (index < source.Length)
        {
            int next = source.Slice(index).IndexOf(needle);

            if (next < 0)
                break;

            int toCopy = next + 1;
            source.Slice(index, toCopy).CopyTo(buffer.Slice(written));
            written += toCopy;
            index += toCopy + 1;

            if ((quoteCount -= 2) == 0)
            {
                source.Slice(index).CopyTo(buffer.Slice(written));
                written += source.Length - index;
                return buffer.Slice(0, written);
            }
        }

        return InvalidUnescape<T>();
    }

    public static ReadOnlySpan<T> UnescapeUncommon<T>(
        ReadOnlySpan<T> source,
        Span<T> buffer,
        T quote,
        int quoteCount)
        where T : unmanaged, IEquatable<T>
    {
        int written = 0;
        bool previous = false;

        foreach (var token in source)
        {
            if (token.Equals(quote))
            {
                if (previous)
                {
                    previous = false;
                    continue;
                }

                previous = true;
            }

            buffer[written++] = token;
        }

        if (previous)
            return InvalidUnescape<T>();

        return buffer.Slice(0, written);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ReadOnlySpan<T> InvalidUnescape<T>()
        where T : unmanaged, IEquatable<T>
    {
        throw new InvalidDataException();
    }
}
