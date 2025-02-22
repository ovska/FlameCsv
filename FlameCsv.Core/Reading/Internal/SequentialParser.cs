using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Reading.Internal;

internal static class SequentialParser<T> where T : unmanaged, IBinaryInteger<T>
{
    public static bool CanRead(int dataLength) => dataLength >= 2;

    public static int Core(
        ref readonly CsvDialect<T> dialect,
        NewlineBuffer<T> newlineArg,
        scoped ReadOnlySpan<T> data,
        scoped Span<Meta> metaBuffer)
    {
        newlineArg.AssertInitialized();
        Debug.Assert(newlineArg.Length != 0);
        Debug.Assert(!metaBuffer.IsEmpty);
        Debug.Assert(CanRead(data.Length));

        T quote = dialect.Quote;
        T delimiter = dialect.Delimiter;
        T newlineFirst = newlineArg.First;
        T newlineSecond = newlineArg.Second;
        nuint newlineLength = (nuint)newlineArg.Length;

        ref T first = ref MemoryMarshal.GetReference(data);
        nuint runningIndex = 0;
        uint quotesConsumed = 0;

        // leave 1 space extra to the end so we can check 2-token newlines and double quotes inside a string
        // ensure the unrolled end doesn't overflow for length <= 8
        nuint searchSpaceEnd = (nuint)data.Length - 1 - 1;
        nuint unrolledEnd = (nuint)Math.Max(0, (nint)searchSpaceEnd - 8);

        ref Meta currentMeta = ref MemoryMarshal.GetReference(metaBuffer);
        ref readonly Meta metaEnd = ref Unsafe.Add(ref MemoryMarshal.GetReference(metaBuffer), metaBuffer.Length);

        while (Unsafe.IsAddressLessThan(in currentMeta, in metaEnd))
        {
            while (runningIndex < unrolledEnd)
            {
                if (Unsafe.Add(ref first, runningIndex + 0) == quote ||
                    Unsafe.Add(ref first, runningIndex + 0) == delimiter ||
                    Unsafe.Add(ref first, runningIndex + 0) == newlineFirst)
                {
                    goto Found;
                }

                if (Unsafe.Add(ref first, runningIndex + 1) == quote ||
                    Unsafe.Add(ref first, runningIndex + 1) == delimiter ||
                    Unsafe.Add(ref first, runningIndex + 1) == newlineFirst)
                {
                    goto Found1;
                }

                if (Unsafe.Add(ref first, runningIndex + 2) == quote ||
                    Unsafe.Add(ref first, runningIndex + 2) == delimiter ||
                    Unsafe.Add(ref first, runningIndex + 2) == newlineFirst)
                {
                    goto Found2;
                }

                if (Unsafe.Add(ref first, runningIndex + 3) == quote ||
                    Unsafe.Add(ref first, runningIndex + 3) == delimiter ||
                    Unsafe.Add(ref first, runningIndex + 3) == newlineFirst)
                {
                    goto Found3;
                }

                if (Unsafe.Add(ref first, runningIndex + 4) == quote ||
                    Unsafe.Add(ref first, runningIndex + 4) == delimiter ||
                    Unsafe.Add(ref first, runningIndex + 4) == newlineFirst)
                {
                    goto Found4;
                }

                if (Unsafe.Add(ref first, runningIndex + 5) == quote ||
                    Unsafe.Add(ref first, runningIndex + 5) == delimiter ||
                    Unsafe.Add(ref first, runningIndex + 5) == newlineFirst)
                {
                    goto Found5;
                }

                if (Unsafe.Add(ref first, runningIndex + 6) == quote ||
                    Unsafe.Add(ref first, runningIndex + 6) == delimiter ||
                    Unsafe.Add(ref first, runningIndex + 6) == newlineFirst)
                {
                    goto Found6;
                }

                if (Unsafe.Add(ref first, runningIndex + 7) == quote ||
                    Unsafe.Add(ref first, runningIndex + 7) == delimiter ||
                    Unsafe.Add(ref first, runningIndex + 7) == newlineFirst)
                {
                    goto Found7;
                }

                runningIndex += 8;
            }

            while (runningIndex <= searchSpaceEnd)
            {
                if (Unsafe.Add(ref first, runningIndex) == quote ||
                    Unsafe.Add(ref first, runningIndex) == delimiter ||
                    Unsafe.Add(ref first, runningIndex) == newlineFirst)
                {
                    goto Found;
                }

                runningIndex++;
            }

            // ran out of data
            break;

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
                currentMeta = Meta.RFC((int)runningIndex, quotesConsumed, isEOL: false, (int)newlineLength);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                runningIndex++;
                quotesConsumed = 0;
                continue;
            }

            //todo: check newline first
            if (Unsafe.Add(ref first, runningIndex) == quote)
            {
                quotesConsumed++;
                runningIndex++;
                goto ReadString;
            }

            Debug.Assert(Unsafe.Add(ref first, runningIndex) == newlineFirst);

            // found CR but not LF
            if (newlineLength != 1 && Unsafe.Add(ref first, runningIndex + 1) != newlineSecond)
            {
                runningIndex++;
                continue;
            }

            currentMeta = Meta.RFC((int)runningIndex, quotesConsumed, isEOL: true, (int)newlineLength);
            currentMeta = ref Unsafe.Add(ref currentMeta, 1);
            runningIndex++;
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
                if (Unsafe.Add(ref first, runningIndex) == delimiter ||
                    Unsafe.Add(ref first, runningIndex) == newlineFirst)
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
            while (runningIndex <= unrolledEnd)
            {
                if (Unsafe.Add(ref first, runningIndex + 0) == quote) goto FoundQuote;
                if (Unsafe.Add(ref first, runningIndex + 1) == quote) goto FoundQuote1;
                if (Unsafe.Add(ref first, runningIndex + 2) == quote) goto FoundQuote2;
                if (Unsafe.Add(ref first, runningIndex + 3) == quote) goto FoundQuote3;
                if (Unsafe.Add(ref first, runningIndex + 4) == quote) goto FoundQuote4;
                if (Unsafe.Add(ref first, runningIndex + 5) == quote) goto FoundQuote5;
                if (Unsafe.Add(ref first, runningIndex + 6) == quote) goto FoundQuote6;
                if (Unsafe.Add(ref first, runningIndex + 7) == quote) goto FoundQuote7;
                runningIndex += 8;
            }

            while (runningIndex <= searchSpaceEnd)
            {
                if (Unsafe.Add(ref first, runningIndex) == quote) goto FoundQuote;
                runningIndex++;
            }

            // ran out of data
            break;
        }

        return (int)Unsafe.ByteOffset(in MemoryMarshal.GetReference(metaBuffer), in currentMeta) /
            Unsafe.SizeOf<Meta>();
    }
}
