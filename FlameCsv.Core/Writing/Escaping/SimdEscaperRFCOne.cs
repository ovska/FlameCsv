using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Writing.Escaping;

internal readonly struct SimdEscaperRFCOne<T, TVector> : ISimdEscaper<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    private readonly TVector _quote;
    private readonly TVector _delimiter;
    private readonly TVector _newline;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimdEscaperRFCOne(T quote, T delimiter, T newline)
    {
        Debug.Assert(TVector.IsSupported);
        Debug.Assert(TVector.Count == 32);

        _quote = TVector.Create(quote);
        _delimiter = TVector.Create(delimiter);
        _newline = TVector.Create(newline);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint FindEscapable(ref readonly TVector value, ref TVector needsQuoting)
    {
        TVector hasQuote = TVector.Equals(value, _quote);
        TVector hasDelimiterOrNewline = TVector.Equals(value, _delimiter) | TVector.Equals(value, _newline);
        uint mask = (uint)hasQuote.ExtractMostSignificantBits();
        needsQuoting |= (hasQuote | hasDelimiterOrNewline);
        return mask;
    }
}
