using System.Collections;
using System.Runtime.CompilerServices;

namespace FlameCsv;

public struct ValueList<T> : IReadOnlyList<T> where T : class
{
    [InlineArray(ArrayThreshold)]
    internal struct Inliner
    {
        private T _element0;
    }

    private const int ArrayThreshold = 16;

    public int Size { get; private set; }
    int IReadOnlyCollection<T>.Count { get; }

    readonly T IReadOnlyList<T>.this[int index] => this[index];

    private Inliner _inliner;
    private T[]? _array;

    public readonly T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)Size, nameof(index));

            if (index < ArrayThreshold)
            {
                return _inliner[index];
            }

            return _array![index - ArrayThreshold];
        }
    }

    public void Add(T func)
    {
        if (Size < ArrayThreshold)
        {
            _inliner[Size] = func;
        }
        else
        {
            if (_array is null)
            {
                _array = new T[ArrayThreshold];
            }
            else if (_array.Length <= (Size - ArrayThreshold))
            {
                Array.Resize(ref _array, _array.Length * 2);
            }

            _array[Size - ArrayThreshold] = func;
        }

        Size++;
    }

    public Enumerator GetEnumerator() => new(_inliner, _array, Size);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public struct Enumerator : IEnumerator<T>
    {
        private Inliner _inliner;
        private readonly T[]? _array;
        private readonly int _size;
        private int _index;

        public T Current { readonly get; private set; }

        readonly object IEnumerator.Current => Current;

        internal Enumerator(Inliner inliner, T[]? array, int size)
        {
            _inliner = inliner;
            _array = array;
            _size = size;
            Current = default!;
        }

        public bool MoveNext()
        {
            if (_index < _size)
            {
                Current = _index < ArrayThreshold
                    ? _inliner[_index]
                    : _array![_index - ArrayThreshold];

                _index++;
                return true;
            }

            return false;
        }

        public readonly void Dispose()
        {
        }

        public readonly void Reset()
        {
        }
    }
}
