using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
internal sealed class ScalarTokenizer<T, TCRLF, TQuote> : CsvScalarTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TCRLF : struct, IConstant
    where TQuote : struct, IConstant
{
    private readonly T _quote;
    private readonly T _delimiter;
    private EnumeratorStack _lut;

    public ScalarTokenizer(CsvOptions<T> options)
    {
        if (typeof(T) != typeof(byte) && typeof(T) != typeof(char))
        {
            throw Token<T>.NotSupported;
        }

        Check.Equal(TCRLF.Value, options.Newline.IsCRLF(), "CRLF constant must match newline option.");
        Check.Equal(TQuote.Value, options.Quote.HasValue, "Quote constant must match presence of quote char.");

        _quote = T.CreateTruncating(options.Quote.GetValueOrDefault());
        _delimiter = T.CreateTruncating(options.Delimiter);

        _lut = default; // zero init
        Span<byte> lut = _lut;

        // for bytes, store a value directly.
        if (options.Quote.HasValue)
        {
            lut[(byte)options.Quote.Value] = 1;
        }

        lut[(byte)options.Delimiter] = 1;
        lut['\n'] = 1;

        if (TCRLF.Value)
        {
            lut['\r'] = 1;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(Span<uint> destination, int startIndex, ReadOnlySpan<T> data, bool readToEnd)
    {
        if (data.IsEmpty || data.Length <= startIndex)
        {
            return 0;
        }

        T quote = _quote;
        T delimiter = _delimiter;
        ref byte lut = ref _lut.elem0;

        ref T first = ref MemoryMarshal.GetReference(data);
        nuint index = (nuint)startIndex;
        uint quotesConsumed = 0;

        scoped ref uint dstField = ref MemoryMarshal.GetReference(destination);
        nuint fieldIndex = 0;

        // offset ends by 1 so quote pairs and CRLF can be checked
        nuint searchSpaceEnd = (nuint)Math.Max(0, data.Length - 1);
        nuint unrolledEnd = (nuint)Math.Max(0, data.Length - 5);

        while (fieldIndex < (nuint)destination.Length)
        {
            while (index < unrolledEnd)
            {
                if (IsAny(ref lut, Unsafe.Add(ref first, index)))
                {
                    goto Found;
                }

                if (IsAny(ref lut, Unsafe.Add(ref first, index + 1)))
                {
                    index += 1;
                    goto Found;
                }

                if (IsAny(ref lut, Unsafe.Add(ref first, index + 2)))
                {
                    index += 2;
                    goto Found;
                }

                if (IsAny(ref lut, Unsafe.Add(ref first, index + 3)))
                {
                    index += 3;
                    goto Found;
                }

                index += 4;
            }

            while (index < searchSpaceEnd)
            {
                if (IsAny(ref lut, Unsafe.Add(ref first, index)))
                {
                    goto Found;
                }

                index++;
            }

            // ran out of data
            goto EndOfData;

            Found:
            if (TQuote.Value && Unsafe.Add(ref first, index) == quote)
            {
                quotesConsumed++;
                index++;
                goto ReadString;
            }

            FoundNonQuote:
            ref T current = ref Unsafe.Add(ref first, index);
            uint flag = 0;

            if (current != delimiter)
            {
                flag = TCRLF.Value && Bithacks.IsCRLF(ref current) ? Field.IsCRLF : Field.IsEOL;
            }

            Unsafe.Add(ref dstField, fieldIndex) = (uint)index | flag | Bithacks.GetQuoteFlags(quotesConsumed);
            fieldIndex++;
            quotesConsumed = 0;
            index += TCRLF.Value ? (1 + ((flag >> 30) & 1)) : 1;
            continue;

            FoundQuote:
            Check.True(TQuote.Value, "Quote must be set if we reach FoundQuote.");
            if (TQuote.Value)
            {
                Check.NotNull(quote, "Quote must be set if we reach this point!");

                // found just a single quote in a string?
                if (Unsafe.Add(ref first, index + 1) != quote)
                {
                    quotesConsumed++;
                    index++;

                    T next = Unsafe.Add(ref first, index);

                    // quotes should be followed by delimiters or newlines
                    if (next == delimiter || IsAnyNewline(next))
                    {
                        goto FoundNonQuote;
                    }

                    continue;
                }

                // two consecutive quotes, continue
                Check.Equal(quotesConsumed % 2, 1u);
                quotesConsumed += 2;
                index += 2;
            }

            ReadString:
            Check.True(TQuote.Value, "Quote must be set if we reach ReadString.");
            if (TQuote.Value)
            {
                Check.Equal(quotesConsumed % 2, 1u);

                while (index < unrolledEnd)
                {
                    if (quote == Unsafe.Add(ref first, index))
                    {
                        goto FoundQuote;
                    }

                    if (quote == Unsafe.Add(ref first, index + 1))
                    {
                        index += 1;
                        goto FoundQuote;
                    }

                    if (quote == Unsafe.Add(ref first, index + 2))
                    {
                        index += 2;
                        goto FoundQuote;
                    }

                    if (quote == Unsafe.Add(ref first, index + 3))
                    {
                        index += 3;
                        goto FoundQuote;
                    }

                    index += 4;
                }

                while (index < searchSpaceEnd)
                {
                    if (Unsafe.Add(ref first, index) == quote)
                        goto FoundQuote;
                    index++;
                }
            }

            // ran out of data
            EndOfData:
            if (!readToEnd)
            {
                break;
            }

            // data ended in a trailing newline?
            if (
                fieldIndex > 0
                && (Field.IsEOL & Unsafe.Add(ref dstField, fieldIndex - 1)) != 0
                && Field.NextStartCRLFAware(Unsafe.Add(ref dstField, fieldIndex - 1)) == data.Length
            )
            {
                break;
            }

            // need to process the final token (unless it was skipped with CRLF)
            if ((nint)index == (data.Length - 1))
            {
                T final = Unsafe.Add(ref first, index);

                if (IsAnyNewline(final))
                {
                    // this can only be a 1-token newline, omit the newline kind as the offset is always 1
                    Unsafe.Add(ref dstField, fieldIndex) =
                        (uint)index | Field.IsEOL | Bithacks.GetQuoteFlags(quotesConsumed);
                    fieldIndex++;
                    break;
                }

                if (final == delimiter)
                {
                    Unsafe.Add(ref dstField, fieldIndex) = (uint)index | Bithacks.GetQuoteFlags(quotesConsumed);
                    quotesConsumed = 0;
                    fieldIndex++;
                }
                else if (TQuote.Value && final == quote)
                {
                    quotesConsumed++;
                }
            }

            // add shadow EOL
            Unsafe.Add(ref dstField, fieldIndex) =
                ((uint)index + 1) | Field.IsEOL | Bithacks.GetQuoteFlags(quotesConsumed);
            fieldIndex++;
            break;
        }

        return (int)fieldIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAny(ref byte lut, T value)
    {
        // for bytes, valid values have a non-zero LUT entry
        if (typeof(T) == typeof(byte))
        {
            return Unsafe.BitCast<byte, bool>(Unsafe.Add(ref lut, (uint)Unsafe.BitCast<T, byte>(value)));
        }

        if (typeof(T) == typeof(char))
        {
            // interleave LUT load with the ascii check
            uint c = Unsafe.BitCast<T, char>(value);
            bool x = Unsafe.BitCast<byte, bool>(Unsafe.Add(ref lut, (byte)c));
            bool y = (c & 0xFF00) == 0; // ensure ASCII, compiles to test ecx, 0xff00
            return x & y;
        }

        throw Token<T>.NotSupported;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAnyNewline(T value)
    {
        return value == T.CreateTruncating('\n') || (TCRLF.Value && value == T.CreateTruncating('\r'));
    }
}
