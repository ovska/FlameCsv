using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;
using FlameCsv.Utilities;

namespace FlameCsv.Extensions;

internal static class EnumerationExtensions
{
    public static Enumerator<T, MetaFieldReader<T>> GetEnumerator<T>(this MetaFieldReader<T> reader)
        where T : unmanaged, IBinaryInteger<T>
    {
        return new(reader);
    }

    public static Enumerator<T, BufferFieldReader<T>> GetEnumerator<T>(this BufferFieldReader<T> reader)
        where T : unmanaged, IBinaryInteger<T>
    {
        return new(reader);
    }

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref struct Enumerator<T, TReader>(TReader reader) : IEnumerator<ReadOnlySpan<T>>
        where T : unmanaged, IBinaryInteger<T>
        where TReader : ICsvRecordFields<T>, allows ref struct
    {
        private TReader _reader = reader;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_index < _reader.FieldCount)
            {
                Current = _reader[_index++];
                return true;
            }

            return false;
        }

        public void Reset() => _index = 0;

        public ReadOnlySpan<T> Current { get; private set; }

        object IEnumerator.Current => throw new NotSupportedException();

        void IDisposable.Dispose()
        {
        }
    }
}
