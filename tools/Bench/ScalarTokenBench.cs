#if false

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Benchmark;

[HideColumns("Error", "RatioSD")]
public class ScalarTokenBench
{
    public enum ParserNewline
    {
        LF,
        CRLF,
        LF_With_CRLF,
    }

    [Params(false, true)]
    public bool Chars { get; set; }

    [Params(true, false)]
    public bool Quoted { get; set; }

    [Params(
        [
            /**/
            ParserNewline.LF,
            ParserNewline.CRLF,
            // ParserNewline.LF_With_CRLF,
        ]
    )]
    public ParserNewline Newline { get; set; }

    public bool DataIsCRLF => Newline == ParserNewline.CRLF;
    public bool TokenizerIsLF => Newline == ParserNewline.LF;

    private static readonly int[] _eolBuffer = new int[24 * 65535];
    private static readonly Meta[] _metaBuffer = new Meta[24 * 65535];
    private static readonly string _chars0LF = File.ReadAllText("Comparisons/Data/65K_Records_Data.csv");
    private static readonly string _chars1LF = File.ReadAllText("Comparisons/Data/SampleCSVFile_556kb_4x.csv");
    private static readonly byte[] _bytes0LF = Encoding.UTF8.GetBytes(_chars0LF);
    private static readonly byte[] _bytes1LF = Encoding.UTF8.GetBytes(_chars1LF);
    private static readonly string _chars0CRLF = _chars0LF.ReplaceLineEndings("\r\n");
    private static readonly string _chars1CRLF = _chars1LF.ReplaceLineEndings("\r\n");
    private static readonly byte[] _bytes0CRLF = Encoding.UTF8.GetBytes(_chars0CRLF);
    private static readonly byte[] _bytes1CRLF = Encoding.UTF8.GetBytes(_chars1CRLF);

    private string CharData => Quoted ? (DataIsCRLF ? _chars1CRLF : _chars1LF) : (DataIsCRLF ? _chars0CRLF : _chars0LF);

    private byte[] ByteData => Quoted ? (DataIsCRLF ? _bytes1CRLF : _bytes1LF) : (DataIsCRLF ? _bytes0CRLF : _bytes0LF);

    private static readonly CsvOptions<char> _dCharLF = new CsvOptions<char>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private static readonly CsvOptions<byte> _dByteCRLF = new CsvOptions<byte>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.CRLF,
    };

    private static readonly CsvOptions<byte> _dByteLF = new CsvOptions<byte>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.LF,
    };

    private static readonly CsvOptions<char> _dCharCRLF = new CsvOptions<char>
    {
        Delimiter = ',',
        Quote = '"',
        Newline = CsvNewline.CRLF,
    };

    private readonly ScalarTokenizer<char, NewlineLF> _t128LF = new(_dCharLF);
    private readonly ScalarTokenizer<byte, NewlineLF> _t128bLF = new(_dByteLF);
    private readonly ScalarTokenizer<char, NewlineCRLF> _t128CRLF = new(_dCharCRLF);
    private readonly ScalarTokenizer<byte, NewlineCRLF> _t128bCRLF = new(_dByteCRLF);
    private readonly OldScalar<char, NewlineLF> _tOld128LF = new(_dCharLF);
    private readonly OldScalar<byte, NewlineLF> _tOld128bLF = new(_dByteLF);
    private readonly OldScalar<char, NewlineCRLF> _tOld128CRLF = new(_dCharCRLF);
    private readonly OldScalar<byte, NewlineCRLF> _tOld128bCRLF = new(_dByteCRLF);

    [Benchmark(Baseline = true)]
    public void Old()
    {
        var rb = new RecordBuffer();
        rb.GetFieldArrayRef() = _metaBuffer;
        rb.UnsafeGetEOLArrayRef() = _eolBuffer;

        if (Chars)
        {
            CsvTokenizer<char> tokenizer = TokenizerIsLF ? _tOld128LF : _tOld128CRLF;
            _ = tokenizer.Tokenize(rb, CharData, false);
        }
        else
        {
            CsvTokenizer<byte> tokenizer = TokenizerIsLF ? _tOld128bLF : _tOld128bCRLF;
            _ = tokenizer.Tokenize(rb, ByteData, false);
        }
    }

    [Benchmark]
    public void LUT()
    {
        var rb = new RecordBuffer();
        rb.GetFieldArrayRef() = _metaBuffer;
        rb.UnsafeGetEOLArrayRef() = _eolBuffer;

        if (Chars)
        {
            CsvTokenizer<char> tokenizer = TokenizerIsLF ? _t128LF : _t128CRLF;
            _ = tokenizer.Tokenize(rb, CharData, false);
        }
        else
        {
            CsvTokenizer<byte> tokenizer = TokenizerIsLF ? _t128bLF : _t128bCRLF;
            _ = tokenizer.Tokenize(rb, ByteData, false);
        }
    }

    [SkipLocalsInit]
    private sealed class OldScalar<T, TNewline>(CsvOptions<T> options) : CsvTokenizer<T>
        where T : unmanaged, IBinaryInteger<T>
        where TNewline : INewline
    {
        private readonly T _quote = T.CreateTruncating(options.Quote);
        private readonly T _delimiter = T.CreateTruncating(options.Delimiter);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public override bool Tokenize(RecordBuffer recordBuffer, ReadOnlySpan<T> data, bool readToEnd)
        {
            Span<Meta> metaBuffer = recordBuffer.GetUnreadBuffer(0, out int startIndex);

            if (data.IsEmpty || data.Length <= startIndex)
            {
                return false;
            }

            Span<int> offsets = recordBuffer.GetEOLBuffer();
            scoped NewlineHolder holder = new(offsets, metaBuffer);

            T quote = _quote;
            T delimiter = _delimiter;

            ref T first = ref MemoryMarshal.GetReference(data);
            nuint runningIndex = (nuint)startIndex;
            uint quotesConsumed = 0;

            // offset ends -2 so we can check for \r\n and "" without bounds checks
            nuint searchSpaceEnd = (nuint)Math.Max(0, data.Length - 2);

            ref Meta currentMeta = ref MemoryMarshal.GetReference(metaBuffer);
            ref readonly Meta metaEnd = ref Unsafe.Add(ref MemoryMarshal.GetReference(metaBuffer), metaBuffer.Length);

            while (Unsafe.IsAddressLessThan(in currentMeta, in metaEnd))
            {
                while (runningIndex <= searchSpaceEnd)
                {
                    bool found =
                        Unsafe.Add(ref first, runningIndex) == quote
                        || Unsafe.Add(ref first, runningIndex) == delimiter
                        || TNewline.IsNewline(Unsafe.Add(ref first, runningIndex));

                    if (found)
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

                NewlineKind kind = TNewline.IsNewline(delimiter, ref Unsafe.Add(ref first, runningIndex));
                currentMeta = Meta.Create((int)runningIndex, quotesConsumed, kind);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                holder.TryAdvance(kind, in currentMeta);
                runningIndex += (uint)kind & (uint)NewlineKind.LengthMask;
                quotesConsumed = 0;
                continue;

                FoundQuote:
                // found just a single quote in a string?
                if (Unsafe.Add(ref first, runningIndex + 1) != quote)
                {
                    quotesConsumed++;
                    runningIndex++;

                    // quotes should be followed by delimiters or newlines
                    if (
                        Unsafe.Add(ref first, runningIndex) == delimiter
                        || TNewline.IsNewline(Unsafe.Add(ref first, runningIndex))
                    )
                    {
                        goto Found;
                    }

                    continue;
                }

                // two consecutive quotes, continue
                quotesConsumed += 2;
                runningIndex += 2;

                ReadString:
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
                    !Unsafe.AreSame(in MemoryMarshal.GetReference(metaBuffer), in currentMeta)
                    && holder.IsLastEOL(in Unsafe.Add(ref currentMeta, -1))
                    && Unsafe.Add(ref currentMeta, -1).NextStart == data.Length
                )
                {
                    break;
                }

                // need to process the final token (unless it was skipped with CRLF)
                if ((nint)runningIndex == (data.Length - 1))
                {
                    T final = Unsafe.Add(ref first, runningIndex);

                    if (TNewline.IsNewline(final))
                    {
                        // this can only be a 1-token newline, omit the newline kind as the offset is always 1
                        currentMeta = Meta.Create((int)runningIndex, quotesConsumed);
                        currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                        holder.Advance(in currentMeta);
                        break;
                    }

                    if (final == delimiter)
                    {
                        currentMeta = Meta.Create((int)runningIndex, quotesConsumed);
                        currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                        quotesConsumed = 0;
                    }
                    else if (final == quote)
                    {
                        quotesConsumed++;
                    }
                }

                currentMeta = Meta.Create((int)runningIndex + 1, quotesConsumed, kind: NewlineKind.EOF);
                currentMeta = ref Unsafe.Add(ref currentMeta, 1);
                holder.Advance(in currentMeta); // add a shadow EOL in case data didn't end with one
                break;
            }

            int fields = (int)(
                (nuint)Unsafe.ByteOffset(in holder.firstMeta, in currentMeta) / (uint)Unsafe.SizeOf<Meta>()
            );
            int eolCount = holder.GetCount();
            recordBuffer.SetFieldsRead(recordCount: eolCount, fieldCount: fields);
            return fields > 0;
        }
    }
}
#endif
