using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Reading;

internal struct CsvSequenceReader<T> where T : unmanaged, IEquatable<T>
{
    private SequencePosition _currentPosition;
    private SequencePosition _nextPosition;
    private bool _moreData;
    private readonly long _length;

    /// <summary>
    /// Create a <see cref="SequenceReader{T}"/> over the given <see cref="ReadOnlySequence{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvSequenceReader(in ReadOnlySequence<T> sequence)
    {
        CurrentIndex = 0;
        Consumed = 0;
        Sequence = sequence;
        _currentPosition = sequence.Start;
        _nextPosition = sequence.Start;
        _length = -1;

        _ = sequence.TryGet(ref _nextPosition, out ReadOnlyMemory<T> first, advance: true);
        Current = first;
        _moreData = first.Length > 0;

        if (!_moreData && !sequence.IsSingleSegment)
        {
            _moreData = true;
            GetNextMemory();
        }
    }

    /// <summary>
    /// True when there is no more data in the <see cref="Sequence"/>.
    /// </summary>
    public readonly bool End => !_moreData;

    /// <summary>
    /// The underlying <see cref="ReadOnlySequence{T}"/> for the reader.
    /// </summary>
    public ReadOnlySequence<T> Sequence { get; }

    /// <summary>
    /// Gets the unread portion of the <see cref="Sequence"/>.
    /// </summary>
    /// <value>
    /// The unread portion of the <see cref="Sequence"/>.
    /// </value>
    public readonly ReadOnlySequence<T> UnreadSequence => Sequence.Slice(Position);

    /// <summary>
    /// The current position in the <see cref="Sequence"/>.
    /// </summary>
    public readonly SequencePosition Position => Sequence.GetPosition(CurrentIndex, _currentPosition);

    /// <summary>
    /// The current segment in the <see cref="Sequence"/> as a span.
    /// </summary>
    public ReadOnlyMemory<T> Current { get; private set; }

    /// <summary>
    /// The index in the <see cref="Current"/>.
    /// </summary>
    public int CurrentIndex { get; internal set; }

    /// <summary>
    /// The unread portion of the <see cref="Current"/>.
    /// </summary>
    public readonly ReadOnlyMemory<T> Unread
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Current.Slice(CurrentIndex);
    }

    /// <summary>
    /// The total number of <typeparamref name="T"/>'s processed by the reader.
    /// </summary>
    public long Consumed { get; internal set; }

    /// <summary>
    /// Remaining <typeparamref name="T"/>'s in the reader's <see cref="Sequence"/>.
    /// </summary>
    public readonly long Remaining => Length - Consumed;

    /// <summary>
    /// Count of <typeparamref name="T"/> in the reader's <see cref="Sequence"/>.
    /// </summary>
    public readonly long Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_length < 0)
            {
                // Cast-away readonly to initialize lazy field
                Unsafe.AsRef(in _length) = Sequence.Length;
            }
            return _length;
        }
    }

    /// <summary>
    /// Get the next segment with available data, if any.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GetNextMemory()
    {
        if (!Sequence.IsSingleSegment)
        {
            SequencePosition previousNextPosition = _nextPosition;
            while (Sequence.TryGet(ref _nextPosition, out ReadOnlyMemory<T> memory, advance: true))
            {
                _currentPosition = previousNextPosition;
                if (memory.Length > 0)
                {
                    Current = memory;
                    CurrentIndex = 0;
                    return;
                }
                else
                {
                    Current = default;
                    CurrentIndex = 0;
                    previousNextPosition = _nextPosition;
                }
            }
        }
        _moreData = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AdvanceToEnd()
    {
        if (_moreData)
        {
            Consumed = Length;
            Current = default;
            CurrentIndex = 0;
            _currentPosition = Sequence.End;
            _nextPosition = default;
            _moreData = false;
        }
    }

    /// <summary>Attempts to advance by <param name="count"/></summary>
    /// <returns>True if the segment changed</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdvance(int count, out bool segmentChanged)
    {
        if (Current.Length - CurrentIndex > (uint)count)
        {
            CurrentIndex += count;
            Consumed += count;
            segmentChanged = false;
            return true;
        }
        else
        {
            // Can't satisfy from the current memory
            segmentChanged = true;
            return AdvanceToNextMemory(count);
        }
    }

    /// <summary>Advances by <param name="count"/> to next segment.</summary>
    /// <returns>True if the segment changed</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool AdvanceToNextMemory(int count)
    {
        if (count < 0)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
        }

        if (Length < (Consumed + count))
        {
            AdvanceToEnd();
            return false;
        }

        Consumed += count;
        while (_moreData)
        {
            int remaining = Current.Length - CurrentIndex;

            if (remaining > count)
            {
                CurrentIndex += count;
                count = 0;
                break;
            }

            // As there may not be any further segments we need to
            // push the current index to the end of the span.
            CurrentIndex += remaining;
            count -= remaining;
            Debug.Assert(count >= 0);

            GetNextMemory();

            if (count == 0)
            {
                break;
            }
        }

        if (count != 0)
        {
            // Not enough data left- adjust for where we actually ended and throw
            Consumed -= count;
            ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
        }

        return true;
    }

    /// <summary>
    /// Unchecked helper to avoid unnecessary checks where you know count is valid.
    /// </summary>
    /// <returns>True if the segment changed</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool AdvanceCurrent(int count)
    {
        Debug.Assert(count >= 0, $"AdvanceCurrent called with negative count {count}");
        Debug.Assert(
            CurrentIndex + count <= Current.Length,
            $"AdvanceCurrent called with invalid count {count} (consumed: {CurrentIndex}, remaining: {Current.Length})");

        Consumed += count;
        CurrentIndex += count;
        if (CurrentIndex >= Current.Length)
        {
            GetNextMemory();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Only call this helper if you know that you are advancing in the current span
    /// with valid count and there is no need to fetch the next one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AdvanceWithinSpan(int count)
    {
        Debug.Assert(count >= 0);

        Consumed += count;
        CurrentIndex += count;

        Debug.Assert(CurrentIndex < Current.Length);
    }

    /// <summary>
    /// Check to see if the given <paramref name="next"/> value is next.
    /// </summary>
    /// <param name="next">The value to compare the next items to.</param>
    /// <param name="advancePast">Move past the <paramref name="next"/> value if found.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNext(T next, bool advancePast = false)
    {
        if (End)
            return false;

        if (Current.Span[CurrentIndex].Equals(next))
        {
            if (advancePast)
            {
                AdvanceCurrent(1);
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Check to see if the given <paramref name="next"/> values are next.
    /// </summary>
    /// <param name="next">The span to compare the next items to.</param>
    /// <param name="advancePast">Move past the <paramref name="next"/> values if found.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNext(scoped ReadOnlySpan<T> next, bool advancePast = false)
    {
        ReadOnlySpan<T> unread = Unread.Span;
        if (unread.StartsWith(next))
        {
            if (advancePast)
            {
                AdvanceCurrent(next.Length);
            }
            return true;
        }

        // Only check the slow path if there wasn't enough to satisfy next
        return unread.Length < next.Length && IsNextSlow(next, advancePast);
    }

    private bool IsNextSlow(scoped ReadOnlySpan<T> next, bool advancePast)
    {
        ReadOnlySpan<T> Current = Unread.Span;

        // We should only come in here if we need more data than we have in our current span
        Debug.Assert(Current.Length < next.Length);

        int fullLength = next.Length;
        SequencePosition nextPosition = _nextPosition;

        while (next.StartsWith(Current))
        {
            if (next.Length == Current.Length)
            {
                // Fully matched
                if (advancePast)
                {
                    AdvanceCurrent(fullLength);
                }
                return true;
            }

            // Need to check the next segment
            while (true)
            {
                if (!Sequence.TryGet(ref nextPosition, out ReadOnlyMemory<T> nextSegment, advance: true))
                {
                    // Nothing left
                    return false;
                }

                if (nextSegment.Length > 0)
                {
                    next = next.Slice(Current.Length);
                    Current = nextSegment.Span;
                    if (Current.Length > next.Length)
                    {
                        Current = Current.Slice(0, next.Length);
                    }
                    break;
                }
            }
        }

        return false;
    }
}
