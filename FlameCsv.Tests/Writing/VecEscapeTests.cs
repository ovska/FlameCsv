using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using FlameCsv.Writing;

namespace FlameCsv.Tests.Writing;

public static class VecEscapeTests
{
    public static TheoryData<string, string> Data
        => new()
        {
            { "James |007| Bond, at Her Majesty's Secret Servce", "James ||007|| Bond, at Her Majesty's Secret Servce" },
            {
                "Wilson Jones 1| Hanging DublLock\u00ae Ring Binders",
                "Wilson Jones 1|| Hanging DublLock\u00ae Ring Binders"
            },
            {
                "The |quick| brown |fox| jumps |over| the |lazy| dog",
                "The ||quick|| brown ||fox|| jumps ||over|| the ||lazy|| dog"
            },
            { "012345678901234abcdefghijklmnopqrstuvwxyz|", "012345678901234abcdefghijklmnopqrstuvwxyz||" },
            { "012345678901234,abcdefghijklmnopqrstuvwxyz", "012345678901234,abcdefghijklmnopqrstuvwxyz" },
            { "|012345678901234abcdefghijklmnopqrstuvwxyz", "||012345678901234abcdefghijklmnopqrstuvwxyz" },
            { "|012345678901234abcdefghijklmnopqrstuvwxyz|", "||012345678901234abcdefghijklmnopqrstuvwxyz||" },
        };

    private static readonly NewlineParserOne<char, Vec256Char> _newline = new('\n');

    [Theory]
    [MemberData(nameof(Data))]
    public static void Should_Escape_Char(string input, string expected)
    {
        Impl<char, NewlineParserOne<char, Vec256Char>, Vec256Char>(input, expected, in _newline);
    }

    static void Impl<T, TNewline, TVector>(ReadOnlySpan<T> value, string expected, in TNewline newline)
        where T : unmanaged, IBinaryInteger<T>
        where TNewline : INewline<T, TVector>
        where TVector : struct, ISimdVector<T, TVector>
    {
        Assert.True(TVector.IsSupported);

        Span<uint> bits = EscapeHandler.GetBitBuffer(value.Length, stackalloc uint[128]);

        bool retVal = EscapeHandler.NeedsEscaping<T, TNewline, TVector>(
            value,
            bits,
            T.CreateChecked(','),
            T.CreateChecked('|'),
            in newline,
            out int quoteCount);

        Assert.True(retVal);

        if (quoteCount == 0)
        {
            Assert.DoesNotContain(T.CreateChecked('|'), value.ToArray());
            return;
        }

        string expectedBitmask = string.Join(
            "",
            value.ToArray().Select(x => x == T.CreateChecked('|') ? '1' : '0'));
        string actualBitmask = ToBitString(bits, value.Length);

        Assert.Equal(expectedBitmask, actualBitmask);

        int requiredLength = value.Length + quoteCount;

        // escape in another buffer so we can validate we don't write past the end or before the start
        T[] buffer = new T[TVector.Count + requiredLength + TVector.Count];
        Span<T> destination = buffer.AsSpan(TVector.Count, requiredLength);

        EscapeHandler.Escape(value, destination, bits, T.CreateChecked('|'));

        Assert.Equal(expected, ((ReadOnlySpan<T>)destination).AsPrintableString());

        Assert.All(buffer[..TVector.Count], x => Assert.Equal(T.Zero, x));
        Assert.All(buffer[(TVector.Count + destination.Length)..], x => Assert.Equal(T.Zero, x));
    }

    private static string ToBitString(ReadOnlySpan<uint> bits, int valueLength)
    {
        Span<char> buffer = stackalloc char[32];
        StringBuilder sb = new();

        for (int i = bits.Length - 1; i >= 0; i--)
        {
            Assert.True(bits[i].TryFormat(buffer, out int charsWritten, "B32"));
            Assert.Equal(32, charsWritten);
            buffer.Reverse();
            sb.Insert(0, buffer.ToString());
        }

        return sb.ToString()[^valueLength..];
    }
}
