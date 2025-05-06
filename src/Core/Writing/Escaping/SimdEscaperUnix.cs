using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;

namespace FlameCsv.Writing.Escaping;

internal readonly struct SimdEscaperUnix<T, TVector> : ISimdEscaper<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    private readonly TVector _quote;
    private readonly TVector _escape;
    private readonly TVector _delimiter;
    private readonly TVector _newline1;
    private readonly TVector _newline2;

    public T Escape { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimdEscaperUnix(T escape, T quote, T delimiter, CsvNewline newline)
    {
        Debug.Assert(TVector.IsSupported);
        Debug.Assert(TVector.Count == 32);

        Escape = escape;
        _quote = TVector.Create(quote);
        _escape = TVector.Create(escape);
        _delimiter = TVector.Create(delimiter);

        newline.GetTokens(out T first, out T second);
        _newline1 = TVector.Create(first);
        _newline2 = TVector.Create(second);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint FindEscapable(ref readonly TVector value, ref TVector needsQuoting)
    {
        TVector hasEscapeOrQuote = TVector.Equals(value, _escape) | TVector.Equals(value, _quote);
        TVector hasDelimiterOrNewline =
            TVector.Equals(value, _delimiter) | TVector.Equals(value, _newline1) | TVector.Equals(value, _newline2);
        uint mask = (uint)hasEscapeOrQuote.ExtractMostSignificantBits();
        needsQuoting |= (hasEscapeOrQuote | hasDelimiterOrNewline);
        return mask;
    }
}
