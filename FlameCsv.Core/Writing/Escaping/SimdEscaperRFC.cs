using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Writing.Escaping;

internal readonly struct SimdEscaperRFC<T, TVector> : ISimdEscaper<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    private readonly TVector _quote;
    private readonly TVector _delimiter;
    private readonly TVector _newline1;
    private readonly TVector _newline2;

    public T Escape { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimdEscaperRFC(T quote, T delimiter, T newline1, T newline2)
    {
        Debug.Assert(TVector.IsSupported);
        Debug.Assert(TVector.Count == 32);

        Escape = quote;
        _quote = TVector.Create(quote);
        _delimiter = TVector.Create(delimiter);
        _newline1 = TVector.Create(newline1);
        _newline2 = TVector.Create(newline2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint FindEscapable(ref readonly TVector value, ref TVector needsQuoting)
    {
        TVector hasQuote = TVector.Equals(value, _quote);
        TVector hasDelimiterOrNewline =
            TVector.Equals(value, _delimiter) |
            TVector.Equals(value, _newline1) |
            TVector.Equals(value, _newline2);
        uint mask = (uint)hasQuote.ExtractMostSignificantBits();
        needsQuoting |= (hasQuote | hasDelimiterOrNewline);
        return mask;
    }
}
