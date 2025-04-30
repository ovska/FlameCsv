using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Reading.Internal;

[SkipLocalsInit]
[SuppressMessage("ReSharper", "InlineTemporaryVariable")]
internal sealed class ScalarTokenizer<T, TNewline>(CsvDialect<T> dialect) : CsvTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
    where TNewline : INewline<T>
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public override int Tokenize(Span<Meta> metaBuffer, ReadOnlySpan<T> data, int startIndex, bool readToEnd)
    {
        if (data.IsEmpty || data.Length <= startIndex)
        {
            return 0;
        }

        T quote = dialect.Quote;
        T delimiter = dialect.Delimiter;

        ref T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = (uint)startIndex;
        uint quotesConsumed = 0;
        bool isMultitoken = false;

        // offset ends -2 so we can check for \r\n and "" without bounds checks
        // ensure no underflow
        nuint searchSpaceEnd = (nuint)Math.Max(0, data.Length - 2);
        nuint unrolledEnd = (nuint)Math.Max(0, (nint)searchSpaceEnd - 8);

        ref Meta currentMeta = ref MemoryMarshal.GetReference(metaBuffer);
        ref readonly Meta metaEnd = ref Unsafe.Add(ref MemoryMarshal.GetReference(metaBuffer), metaBuffer.Length);

        while (Unsafe.IsAddressLessThan(in currentMeta, in metaEnd))
        {
            while (runningIndex < unrolledEnd)
            {
                if (
                    Unsafe.Add(ref first, runningIndex + 0) == quote
                    || Unsafe.Add(ref first, runningIndex + 0) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + 0), out isMultitoken)
                )
                {
                    goto Found;
                }

                if (
                    Unsafe.Add(ref first, runningIndex + 1) == quote
                    || Unsafe.Add(ref first, runningIndex + 1) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + 1), out isMultitoken)
                )
                {
                    goto Found1;
                }

                if (
                    Unsafe.Add(ref first, runningIndex + 2) == quote
                    || Unsafe.Add(ref first, runningIndex + 2) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + 2), out isMultitoken)
                )
                {
                    goto Found2;
                }

                if (
                    Unsafe.Add(ref first, runningIndex + 3) == quote
                    || Unsafe.Add(ref first, runningIndex + 3) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + 3), out isMultitoken)
                )
                {
                    goto Found3;
                }

                if (
                    Unsafe.Add(ref first, runningIndex + 4) == quote
                    || Unsafe.Add(ref first, runningIndex + 4) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + 4), out isMultitoken)
                )
                {
                    goto Found4;
                }

                if (
                    Unsafe.Add(ref first, runningIndex + 5) == quote
                    || Unsafe.Add(ref first, runningIndex + 5) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + 5), out isMultitoken)
                )
                {
                    goto Found5;
                }

                if (
                    Unsafe.Add(ref first, runningIndex + 6) == quote
                    || Unsafe.Add(ref first, runningIndex + 6) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + 6), out isMultitoken)
                )
                {
                    goto Found6;
                }

                if (
                    Unsafe.Add(ref first, runningIndex + 7) == quote
                    || Unsafe.Add(ref first, runningIndex + 7) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex + 7), out isMultitoken)
                )
                {
                    goto Found7;
                }

                runningIndex += 8;
            }

            while (runningIndex <= searchSpaceEnd)
            {
                if (
                    Unsafe.Add(ref first, runningIndex) == quote
                    || Unsafe.Add(ref first, runningIndex) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex), out isMultitoken)
                )
                {
                    goto Found;
                }

                runningIndex++;
            }

            // ran out of data
            goto EndOfData;

            Found7:
            runningIndex += 7;
            goto Found;
            Found6:
            runningIndex += 6;
            goto Found;
            Found5:
            runningIndex += 5;
            goto Found;
            Found4:
            runningIndex += 4;
            goto Found;
            Found3:
            runningIndex += 3;
            goto Found;
            Found2:
            runningIndex += 2;
            goto Found;
            Found1:
            runningIndex += 1;
            Found:
            if (Unsafe.Add(ref first, runningIndex) == delimiter)
            {
                currentMeta = Meta.RFC((int)runningIndex, quotesConsumed);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                runningIndex++;
                quotesConsumed = 0;
                continue;
            }

            if (Unsafe.Add(ref first, runningIndex) == quote)
            {
                quotesConsumed++;
                runningIndex++;
                goto ReadString;
            }

            Debug.Assert(TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex), out _));
            int newlineLength = TNewline.GetLength(isMultitoken);
            currentMeta = Meta.EOL((int)runningIndex, quotesConsumed, newlineLength);
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);
            runningIndex += (uint)newlineLength;
            quotesConsumed = 0;
            continue;

            FoundQuote7:
            runningIndex += 7;
            goto FoundQuote;
            FoundQuote6:
            runningIndex += 6;
            goto FoundQuote;
            FoundQuote5:
            runningIndex += 5;
            goto FoundQuote;
            FoundQuote4:
            runningIndex += 4;
            goto FoundQuote;
            FoundQuote3:
            runningIndex += 3;
            goto FoundQuote;
            FoundQuote2:
            runningIndex += 2;
            goto FoundQuote;
            FoundQuote1:
            runningIndex += 1;
            FoundQuote:
            // found just a single quote in a string?
            if (Unsafe.Add(ref first, runningIndex + 1) != quote)
            {
                quotesConsumed++;
                runningIndex++;

                // quotes should be followed by delimiters or newlines
                if (
                    Unsafe.Add(ref first, runningIndex) == delimiter
                    || TNewline.IsNewline(ref Unsafe.Add(ref first, runningIndex), out isMultitoken)
                )
                {
                    goto Found;
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
                if (Unsafe.Add(ref first, runningIndex + 0) == quote)
                    goto FoundQuote;
                if (Unsafe.Add(ref first, runningIndex + 1) == quote)
                    goto FoundQuote1;
                if (Unsafe.Add(ref first, runningIndex + 2) == quote)
                    goto FoundQuote2;
                if (Unsafe.Add(ref first, runningIndex + 3) == quote)
                    goto FoundQuote3;
                if (Unsafe.Add(ref first, runningIndex + 4) == quote)
                    goto FoundQuote4;
                if (Unsafe.Add(ref first, runningIndex + 5) == quote)
                    goto FoundQuote5;
                if (Unsafe.Add(ref first, runningIndex + 6) == quote)
                    goto FoundQuote6;
                if (Unsafe.Add(ref first, runningIndex + 7) == quote)
                    goto FoundQuote7;
                runningIndex += 8;
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

            // data ended in a trailing newline
            if (
                !Unsafe.AreSame(in MemoryMarshal.GetReference(metaBuffer), in currentMeta)
                && Unsafe.Add(ref currentMeta, -1).IsEOL
                && Unsafe.Add(ref currentMeta, -1).NextStart == data.Length
            )
            {
                break;
            }

            // need to process the final token (unless it was skipped with CRLF)
            if (((nint)runningIndex == (data.Length - 1)))
            {
                T final = Unsafe.Add(ref first, runningIndex);

                if (TNewline.IsNewline(final))
                {
                    // this can only be a 1-token newline
                    currentMeta = Meta.EOL((int)runningIndex, quotesConsumed, newlineLength: 1);
                    currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                    break;
                }

                if (final == delimiter)
                {
                    currentMeta = Meta.RFC((int)runningIndex, quotesConsumed);
                    currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                    quotesConsumed = 0;
                }
                else if (final == quote)
                {
                    quotesConsumed++;
                }
            }

            // TODO: ensure this works with trailing LF
            currentMeta = Meta.EOL((int)runningIndex + 1, quotesConsumed, newlineLength: 0);
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);
            break;
        }

        return (int)Unsafe.ByteOffset(in MemoryMarshal.GetReference(metaBuffer), in currentMeta)
            / Unsafe.SizeOf<Meta>();
    }
}
