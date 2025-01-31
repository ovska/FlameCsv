using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;

// ReSharper disable ConvertToAutoPropertyWhenPossible

namespace FlameCsv.Benchmark.Utils;

internal readonly ref struct RawFieldReader<T> : ICsvRecordFields<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly int _newlineLength;
    private readonly ReadOnlySpan<T> _data;
    private readonly ref Meta _firstMeta;
    private readonly int _fieldCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RawFieldReader(ref readonly CsvLine<T> line)
    {
        Options = line.Parser.Options;
        _newlineLength = line.Parser._newline.Length;
        _data = line.Data.Span;
        _firstMeta = ref MemoryMarshal.GetReference(line.Fields);
        _fieldCount = line.Fields.Length - 1;
    }

    public int FieldCount => _fieldCount;

    public CsvOptions<T> Options { get; }

    public ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index > (uint)_fieldCount)
                Throw.Argument_FieldIndex(index, _fieldCount);

            ref Meta meta = ref Unsafe.Add(ref _firstMeta, index + 1);
            int start = Unsafe.Add(ref _firstMeta, index).GetNextStart(_newlineLength);

            return _data[start..meta.End];
        }
    }
}
