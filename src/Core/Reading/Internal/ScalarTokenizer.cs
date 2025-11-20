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
    private readonly T _quote;
    private readonly T _delimiter;
    private EnumeratorStack _lut;

    public ScalarTokenizer(CsvOptions<T> options)
    {
        _quote = T.CreateTruncating(options.Quote);
        _delimiter = T.CreateTruncating(options.Delimiter);

        Span<byte> lut = _lut;
        lut.Clear();

        if (typeof(T) == typeof(byte))
        {
            Debug.Assert(lut.Length == 256, "LUT must be 256 bytes long");

            // for bytes, store a value directly.
            lut[options.Quote] = 1;
            lut[options.Delimiter] = 1;
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
            Debug.Assert(span.Length == 128, "LUT must be 128 chars long");

            // for chars we need to ensure that high bits are zeroed out, so compare the value with itself
            span[0] = char.MaxValue; // in case the data contains zeroes
            span[options.Quote] = options.Quote;
            span[options.Delimiter] = options.Delimiter;
            span['\n'] = '\n';

            if (TCRLF.Value)
            {
                span['\r'] = '\r';
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(FieldBuffer buffer, int startIndex, ReadOnlySpan<T> data, bool readToEnd)
    {
        if (data.IsEmpty || data.Length <= startIndex)
        {
            return 0;
        }

        T quote = _quote;
        T delimiter = _delimiter;

        ref T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = (nuint)startIndex;
        uint quotesConsumed = 0;

        scoped ref uint dstField = ref MemoryMarshal.GetReference(buffer.Fields);
        scoped ref byte dstQuote = ref MemoryMarshal.GetReference(buffer.Quotes);
        nuint fieldIndex = 0;

        // offset ends -2 so we can check for \r\n and "" without bounds checks
        nuint searchSpaceEnd = (nuint)Math.Max(0, data.Length - 2);
        nuint unrolledEnd = (nuint)Math.Max(0, data.Length - 4);

        while (fieldIndex < (nuint)buffer.Fields.Length)
        {
            while (runningIndex < unrolledEnd)
            {
                if (IsAny(Unsafe.Add(ref first, runningIndex)))
                {
                    goto Found;
                }

                if (IsAny(Unsafe.Add(ref first, runningIndex + 1)))
                {
                    runningIndex += 1;
                    goto Found;
                }

                if (IsAny(Unsafe.Add(ref first, runningIndex + 2)))
                {
                    runningIndex += 2;
                    goto Found;
                }

                if (IsAny(Unsafe.Add(ref first, runningIndex + 3)))
                {
                    runningIndex += 3;
                    goto Found;
                }

                runningIndex += 4;
            }

            while (runningIndex <= searchSpaceEnd)
            {
                if (IsAny(Unsafe.Add(ref first, runningIndex)))
                {
                    goto Found;
                }

                runningIndex++;
            }

            // ran out of data
            goto EndOfData;

            Found:
            if (Unsafe.Add(ref first, runningIndex) == quote)
            {
                quotesConsumed++;
                runningIndex++;
                goto ReadString;
            }

            FoundNonQuote:
            ref T current = ref Unsafe.Add(ref first, runningIndex);
            uint flag = 0;

            if (current != delimiter)
            {
                flag = TCRLF.Value && Bithacks.IsCRLF(ref current) ? Field.IsCRLF : Field.IsEOL;
            }

            Field.SaturateQuotes(ref quotesConsumed);

            Unsafe.Add(ref dstField, fieldIndex) = (uint)runningIndex | flag;
            Unsafe.Add(ref dstQuote, fieldIndex) = (byte)quotesConsumed;
            fieldIndex++;
            quotesConsumed = 0;
            runningIndex += TCRLF.Value ? (1 + ((flag >> 30) & 1)) : 1;
            continue;

            FoundQuote:
            // found just a single quote in a string?
            if (Unsafe.Add(ref first, runningIndex + 1) != quote)
            {
                quotesConsumed++;
                runningIndex++;

                T next = Unsafe.Add(ref first, runningIndex);

                // quotes should be followed by delimiters or newlines
                if (next == delimiter || IsAnyNewline(next))
                {
                    goto FoundNonQuote;
                }

                continue;
            }

            // two consecutive quotes, continue
            Debug.Assert(quotesConsumed % 2 == 1);
            quotesConsumed += 2;
            runningIndex += 2;

            ReadString:
            Debug.Assert(quotesConsumed % 2 != 0);

            while (runningIndex < unrolledEnd)
            {
                if (quote == Unsafe.Add(ref first, runningIndex))
                {
                    goto FoundQuote;
                }

                if (quote == Unsafe.Add(ref first, runningIndex + 1))
                {
                    runningIndex += 1;
                    goto FoundQuote;
                }

                if (quote == Unsafe.Add(ref first, runningIndex + 2))
                {
                    runningIndex += 2;
                    goto FoundQuote;
                }

                if (quote == Unsafe.Add(ref first, runningIndex + 3))
                {
                    runningIndex += 3;
                    goto FoundQuote;
                }

                runningIndex += 4;
            }

            while (runningIndex <= searchSpaceEnd)
            {
                if (Unsafe.Add(ref first, runningIndex) == quote)
                    goto FoundQuote;
                runningIndex++;
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
                && Field.NextStart(Unsafe.Add(ref dstField, fieldIndex - 1)) == data.Length
            )
            {
                break;
            }

            // need to process the final token (unless it was skipped with CRLF)
            if ((nint)runningIndex == (data.Length - 1))
            {
                T final = Unsafe.Add(ref first, runningIndex);
                Field.SaturateQuotes(ref quotesConsumed);

                if (IsAnyNewline(final))
                {
                    // this can only be a 1-token newline, omit the newline kind as the offset is always 1
                    Unsafe.Add(ref dstField, fieldIndex) = (uint)runningIndex | Field.IsEOL;
                    Unsafe.Add(ref dstQuote, fieldIndex) = (byte)quotesConsumed;
                    fieldIndex++;
                    break;
                }

                if (final == delimiter)
                {
                    Unsafe.Add(ref dstField, fieldIndex) = (uint)runningIndex;
                    Unsafe.Add(ref dstQuote, fieldIndex) = (byte)quotesConsumed;
                    quotesConsumed = 0;
                    fieldIndex++;
                }
                else if (final == quote)
                {
                    quotesConsumed++;
                }
            }

            Field.SaturateQuotes(ref quotesConsumed);
            Unsafe.Add(ref dstField, fieldIndex) = (uint)++runningIndex | Field.IsEOL; // add shadow EOL
            Unsafe.Add(ref dstQuote, fieldIndex) = (byte)quotesConsumed;
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
