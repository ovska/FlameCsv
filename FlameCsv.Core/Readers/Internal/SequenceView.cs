using System.Buffers;
using System.Diagnostics;
using System.Text;
using FlameCsv.Extensions;

namespace FlameCsv.Readers.Internal;

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
    /// Data in the parameter sequence trimmed according to options.
    /// </summary>
    public ReadOnlyMemory<T> Memory { get; }

    private readonly T[]? _array;
    private readonly bool _clearArray;

    public SequenceView(
        in ReadOnlySequence<T> sequence,
        CsvConfiguration<T> configuration)
    {
        if (sequence.IsSingleSegment)
        {
            Memory = sequence.First;
            _array = default;
            _clearArray = default;
        }
        else
        {
            int length = (int)sequence.Length;
            _array = ArrayPool<T>.Shared.Rent(length);
            sequence.CopyTo(_array);
            Memory = _array.AsMemory(0, length);
            _clearArray = configuration.Security.ClearBuffers();
        }

        Memory = Memory.Trim(configuration.options.Whitespace.Span);
    }

    public void Dispose()
    {
        if (_array is not null)
            ArrayPool<T>.Shared.Return(_array, _clearArray);
    }

#if DEBUG
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private string DebugString
        => typeof(T) == typeof(byte)
            ? Encoding.UTF8.GetString(((ReadOnlyMemory<byte>)(object)Memory).Span)
            : Memory.Span.ToString();
#endif
}
