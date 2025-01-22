using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Writing;

namespace FlameCsv.Benchmark;

public class NeedsEscapingBench
{
    private char[][] _fields = [];
    private readonly char? _escape = null;

    [GlobalSetup]
    public void Setup()
    {
        _ = _escape;
        throw new NotImplementedException();
        // List<char[]> fields = [];
        //
        // var data = File.ReadLines(
        //     "C:/Users/Sipi/source/repos/FlameCsv/FlameCsv.Tests/TestData/SampleCSVFile_556kb.csv",
        //     Encoding.ASCII);
        //
        // IMemoryOwner<char>? buffer = null;
        // char[] unescapeBuffer = new char[1024];
        //
        // using var parser = CsvParser<char>.Create(CsvOptions<char>.Default);
        //
        // foreach (var line in data)
        // {
        //     var meta = parser.GetAsCsvLine(line.AsMemory());
        //     var reader = new CsvFieldReader<char>(CsvOptions<char>.Default, in meta, unescapeBuffer, ref buffer);
        //
        //     while (reader.MoveNext())
        //     {
        //         fields.Add(reader.Current.ToArray());
        //     }
        // }
        //
        // buffer?.Dispose();
        //
        // _fields = fields.ToArray();
    }

    [Benchmark(Baseline = true)]
    public void Old()
    {
        var escaper = new OldEscaper<char>(',', '"', '\r', '\n', 2, default);

        foreach (var field in _fields)
        {
            _ = escaper.MustBeQuoted(field, out _);
        }
    }

    [Benchmark(Baseline = false)]
    public void New()
    {
        var escaper = new RFC4180Escaper<char>('"');
        var searchValues = CsvOptions<char>.Default.Dialect.NeedsQuoting;

        foreach (var field in _fields)
        {
            ReadOnlySpan<char> written = field.AsSpan();

            if (!written.IsEmpty)
            {
                int index = written.IndexOfAny(searchValues);

                if (index != -1)
                {
                    _ = escaper.CountEscapable(written[index..]);
                }
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
