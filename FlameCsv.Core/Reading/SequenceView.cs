using System.Buffers;
using System.Diagnostics;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

/// <summary>
/// A non-allocating view around a <see cref="ReadOnlySequence{T}"/> that copies the
/// data to a pooled buffer if it is multisegment.
/// </summary>
/// <typeparam name="T"></typeparam>
[DebuggerDisplay(@"\{ SequenceView: [{DebugString,nq}] \}")]
internal readonly struct SequenceView<T> : IDisposable
    where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Data in the parameter sequence.
    /// </summary>
    public ReadOnlyMemory<T> Memory { get; }

    /// <summary>
    /// Data in the parameter sequence.
    /// </summary>
    public ReadOnlySpan<T> Span => Memory.Span;

    private readonly T[]? _array;
    private readonly ArrayPool<T>? _pool;

    public SequenceView(
        in ReadOnlySequence<T> sequence,
        ArrayPool<T>? arrayPool)
    {
        if (sequence.IsSingleSegment)
        {
            Memory = sequence.First;
        }
        else
        {
            int length = (int)sequence.Length;
            _pool = arrayPool.AllocatingIfNull();
            _array = _pool.Rent(length);
            sequence.CopyTo(_array);
            Memory = _array.AsMemory(0, length);
        }
    }

    public void Dispose()
    {
        if (_array is not null)
            _pool!.Return(_array);
    }

#if DEBUG
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebugString
        => typeof(T) == typeof(byte)
            ? Encoding.UTF8.GetString(((ReadOnlyMemory<byte>)(object)Memory).Span)
            : Memory.Span.ToString();
#endif
}
