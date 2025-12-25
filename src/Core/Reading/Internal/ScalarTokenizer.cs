using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Intrinsics;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
internal sealed class ScalarTokenizer<T, TCRLF> : CsvScalarTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TCRLF : struct, IConstant
{
    private readonly T? _quote;
    private readonly T _delimiter;
    private EnumeratorStack _lut;

    public ScalarTokenizer(CsvOptions<T> options)
    {
        _quote = options.Quote is { } q ? T.CreateTruncating(q) : null;
        _delimiter = T.CreateTruncating(options.Delimiter);

        _lut = default; // zero init
        Span<byte> lut = _lut;

        if (typeof(T) == typeof(byte))
        {
            Check.Equal(lut.Length, 256);

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
        else
        {
            if (typeof(T) != typeof(char))
            {
                throw Token<T>.NotSupported;
            }

            Span<char> span = MemoryMarshal.Cast<byte, char>((Span<byte>)_lut);
            Check.Equal(span.Length, 128);

            // for chars we need to ensure that high bits are zeroed out, so compare the value with itself
            span[0] = char.MaxValue; // in case the data contains zeroes

            if (options.Quote.HasValue)
            {
                span[options.Quote.Value & 127] = options.Quote.Value;
            }

            span[options.Delimiter & 127] = options.Delimiter;
            span['\n'] = '\n';

            if (TCRLF.Value)
            {
                span['\r'] = '\r';
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(Span<uint> buffer, int startIndex, ReadOnlySpan<T> data, bool readToEnd)
    {
        if (data.IsEmpty || data.Length <= startIndex)
        {
            return 0;
        }

        T? quote = _quote;
        T delimiter = _delimiter;

        ref T first = ref MemoryMarshal.GetReference(data);
        nuint index = (nuint)startIndex;
        uint quotesConsumed = 0;

        scoped ref uint dstField = ref MemoryMarshal.GetReference(buffer);
        nuint fieldIndex = 0;

        // offset ends -2 so we can check for \r\n and "" without bounds checks
        nuint searchSpaceEnd = (nuint)Math.Max(0, data.Length - 2);
        nuint unrolledEnd = (nuint)Math.Max(0, data.Length - 6);

        while (fieldIndex < (nuint)buffer.Length)
        {
            while (index < unrolledEnd)
            {
                if (IsAny(Unsafe.Add(ref first, index)))
                {
                    goto Found;
                }

                if (IsAny(Unsafe.Add(ref first, index + 1)))
                {
                    index += 1;
                    goto Found;
                }

                if (IsAny(Unsafe.Add(ref first, index + 2)))
                {
                    index += 2;
                    goto Found;
                }

                if (IsAny(Unsafe.Add(ref first, index + 3)))
                {
                    index += 3;
                    goto Found;
                }

                index += 4;
            }

            while (index <= searchSpaceEnd)
            {
                if (IsAny(Unsafe.Add(ref first, index)))
                {
                    goto Found;
                }

                index++;
            }

            // ran out of data
            goto EndOfData;

            Found:
            if (Unsafe.Add(ref first, index) == quote)
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

            ReadString:
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

            while (index <= searchSpaceEnd)
            {
                if (Unsafe.Add(ref first, index) == quote)
                    goto FoundQuote;
                index++;
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
                else if (final == quote)
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
    private bool IsAny(T value)
    {
        // for bytes, valid values have a non-zero LUT entry
        if (typeof(T) == typeof(byte))
        {
            return Unsafe.BitCast<byte, bool>(_lut[Unsafe.BitCast<T, byte>(value)]);
        }

        if (typeof(T) != typeof(char))
        {
            throw Token<T>.NotSupported;
        }

        // for chars, the LUT contains the char itself
        // index the 256 byte LUT with the lowest 7 bits, and compare
        // this way weird chars like (',' | (',' << 8)) don't match
        uint c = Unsafe.BitCast<T, char>(value);
        return Unsafe.Add(ref Unsafe.As<byte, char>(ref _lut.elem0), c & 127u) == c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAnyNewline(T value)
    {
        return value == T.CreateTruncating('\n') || (TCRLF.Value && value == T.CreateTruncating('\r'));
    }
}
