using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Writing.Escaping;

internal interface IEscapeTokens<out T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    /// <summary>
    /// Returns a bitmask for characters in <paramref name="value"/> that need to be escape.
    /// </summary>
    /// <param name="value">Value to search in</param>
    /// <param name="needsQuoting">Vector reference of values that require the value to be quoted.</param>
    /// <returns>Bitmask containing characters that need to be escaped</returns>
    [Pure] uint FindEscapable(ref readonly TVector value, ref TVector needsQuoting);
}

internal readonly struct EscapeTokensRFCOne<T, TVector> : IEscapeTokens<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    private readonly TVector _quote;
    private readonly TVector _delimiter;
    private readonly TVector _newline;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EscapeTokensRFCOne(T quote, T delimiter, T newline)
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

internal readonly struct EscapeTokensRFCTwo<T, TVector> : IEscapeTokens<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    private readonly TVector _quote;
    private readonly TVector _delimiter;
    private readonly TVector _newline1;
    private readonly TVector _newline2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EscapeTokensRFCTwo(T quote, T delimiter, T newline1, T newline2)
    {
        Debug.Assert(TVector.IsSupported);
        Debug.Assert(TVector.Count == 32);

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

// we are lazy and do only one check for Unix escaping
internal readonly struct EscapeTokensUnix<T, TVector> : IEscapeTokens<T, TVector>
    where T : unmanaged, IBinaryInteger<T>
    where TVector : struct, ISimdVector<T, TVector>
{
    private readonly TVector _quote;
    private readonly TVector _escape;
    private readonly TVector _delimiter;
    private readonly TVector _newline1;
    private readonly TVector _newline2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EscapeTokensUnix(T escape, T quote, T delimiter, in NewlineBuffer<T> newline)
    {
        Debug.Assert(TVector.IsSupported);
        Debug.Assert(TVector.Count == 32);

        _quote = TVector.Create(quote);
        _escape = TVector.Create(escape);
        _delimiter = TVector.Create(delimiter);
        _newline1 = TVector.Create(newline.First);
        _newline2 = TVector.Create(newline.Second);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint FindEscapable(ref readonly TVector value, ref TVector needsQuoting)
    {
        TVector hasEscapeOrQuote = TVector.Equals(value, _escape) | TVector.Equals(value, _quote);
        TVector hasDelimiterOrNewline =
            TVector.Equals(value, _delimiter) |
            TVector.Equals(value, _newline1) |
            TVector.Equals(value, _newline2);
        uint mask = (uint)hasEscapeOrQuote.ExtractMostSignificantBits();
        needsQuoting |= (hasEscapeOrQuote | hasDelimiterOrNewline);
        return mask;
    }
}
