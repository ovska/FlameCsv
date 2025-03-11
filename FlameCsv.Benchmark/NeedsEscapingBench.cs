using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using FlameCsv.Utilities;
using FlameCsv.Writing;

namespace FlameCsv.Benchmark;

public class NeedsEscapingBench
{
    private char[][] _fields = [];

    [GlobalSetup]
    public void Setup()
    {
        List<char[]> fields = [];

        using var data = CsvPipeReader.Create(
            File.OpenRead("C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv"),
            Encoding.UTF8);

        foreach (var record in CsvParser.Create(CsvOptions<char>.Default, data))
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                if (record[i].Length >= 32)
                    fields.Add(record[i].ToArray());
            }
        }

        _fields = fields.ToArray();
    }

    [Benchmark(Baseline = true)]
    public void Scalar()
    {
        var escaper = new RFC4180Escaper<char>('"');
        var searchValues = CsvOptions<char>.Default.Dialect.NeedsQuoting;
        Span<char> buffer = stackalloc char[512];

        foreach (var field in _fields)
        {
            ReadOnlySpan<char> written = field.AsSpan();

            // if (written.Length >= 32)
            {
                int index = written.IndexOfAny(searchValues);

                if (index != -1)
                {
                    int count = escaper.CountEscapable(written[index..]);
                    Escape.Field(ref escaper, field, buffer, count);
                }
            }
        }
    }

    [Benchmark(Baseline = false)]
    public void Simd()
    {
        Span<char> buffer = stackalloc char[512];
        Span<uint> bitBuffer = stackalloc uint[128];
        var newline = new NewlineParserOne<char, Vec256Char>('\n');

        foreach (var field in _fields)
        {
            ReadOnlySpan<char> written = field.AsSpan();
            // if (written.Length < 32) continue;

            var bits = EscapeHandler.GetBitBuffer(written.Length, bitBuffer);
            bool retVal = EscapeHandler.NeedsEscaping<char, NewlineParserOne<char, Vec256Char>, Vec256Char>(
                written,
                bits,
                ',',
                '"',
                in newline,
                out int quoteCount);

            if (retVal)
            {
                EscapeHandler.Escape<char>(written, buffer.Slice(0, written.Length + quoteCount), bits, '"');
            }
        }
    }
}

file readonly struct OldEscaper<T> : IEscaper<T> where T : unmanaged, IBinaryInteger<T>
{
    public T Quote
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _quote;
    }

    public T Escape
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _quote;
    }

    private readonly T _delimiter;
    private readonly T _quote;
    private readonly T _newline1;
    private readonly T _newline2;
    private readonly int _newlineLength;
    private readonly ReadOnlyMemory<T> _whitespace;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public OldEscaper(
        T delimiter,
        T quote,
        T newline1,
        T newline2,
        int newlineLength,
        ReadOnlyMemory<T> whitespace)
    {
        _delimiter = delimiter;
        _quote = quote;
        _newline1 = newline1;
        _newline2 = newline2;
        _newlineLength = newlineLength;
        _whitespace = whitespace;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsEscaping(T value) => value.Equals(_quote);

    public bool SupportsVectorization => throw new NotSupportedException();

    public bool MustBeQuoted(ReadOnlySpan<T> field, out int escapableCount)
    {
        if (field.IsEmpty)
        {
            escapableCount = 0;
            return false;
        }

        int index;

        if (_newlineLength != 1)
        {
            index = field.IndexOfAny(_delimiter, _quote);

            if (index >= 0)
            {
                goto FoundQuoteOrDelimiter;
            }

            escapableCount = 0;
            return field.IndexOf([_newline1, _newline2]) >= 0;
        }

        // Single token newlines can be seeked directly
        index = field.IndexOfAny(_delimiter, _quote, _newline1);

        if (index >= 0)
        {
            goto FoundQuoteOrDelimiter;
        }

        escapableCount = 0;

        if (!_whitespace.IsEmpty)
        {
            ref T first = ref MemoryMarshal.GetReference(field);
            ref T last = ref Unsafe.Add(ref first, field.Length - 1);

            foreach (T token in _whitespace.Span)
            {
                if (first.Equals(token) || last.Equals(token))
                {
                    return true;
                }
            }
        }

        return false;

        FoundQuoteOrDelimiter:
        escapableCount = CountEscapable(field.Slice(index));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastIndexOfEscapable(ReadOnlySpan<T> value) => value.LastIndexOf(Quote);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountEscapable(ReadOnlySpan<T> value) => value.Count(_quote);
}
