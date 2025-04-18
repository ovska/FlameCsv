using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.IO;

internal struct BufferSegmentStack<T> where T : unmanaged, IBinaryInteger<T>
{
    private SegmentAsValueType[] _array;
    private int _size;

    public BufferSegmentStack(int size)
    {
        _array = new SegmentAsValueType[size];
        _size = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop([NotNullWhen(true)] out CsvBufferSegment<T>? result)
    {
        int size = _size - 1;
        SegmentAsValueType[] array = _array;

        if ((uint)size >= (uint)array.Length)
        {
            result = null;
            return false;
        }

        _size = size;
        result = array[size];
        array[size] = default;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(CsvBufferSegment<T> item)
    {
        int size = _size;
        SegmentAsValueType[] array = _array;

        if ((uint)size < (uint)array.Length)
        {
            array[size] = item;
            _size = size + 1;
        }
        else
        {
            PushWithResize(item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PushWithResize(CsvBufferSegment<T> item)
    {
        Array.Resize(ref _array, 2 * _array.Length);
        _array[_size] = item;
        _size++;
    }

    private readonly struct SegmentAsValueType
    {
        private readonly CsvBufferSegment<T> _value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SegmentAsValueType(CsvBufferSegment<T> value) => _value = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SegmentAsValueType(CsvBufferSegment<T> s) => new(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator CsvBufferSegment<T>(SegmentAsValueType s) => s._value;
    }
}
