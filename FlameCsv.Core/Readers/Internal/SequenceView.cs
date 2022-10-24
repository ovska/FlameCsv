using System.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Readers.Internal;

internal readonly ref struct SequenceView<T> where T : unmanaged
{
    public ReadOnlySpan<T> Span { get; }
    private readonly T[]? _array;
    private readonly bool _clearArray;

    public SequenceView(ReadOnlySequence<T> sequence, SecurityLevel security)
    {
        if (sequence.IsSingleSegment)
        {
            Span = sequence.FirstSpan;
            _array = default;
            _clearArray = default;
        }
        else
        {
            int length = (int)sequence.Length;
            _array = ArrayPool<T>.Shared.Rent(length);
            sequence.CopyTo(_array);
            Span = _array.AsSpan(0, length);
            _clearArray = security.ClearBuffers();
        }
    }

    public void Dispose()
    {
        if (_array is not null)
            ArrayPool<T>.Shared.Return(_array, _clearArray);
    }
}
