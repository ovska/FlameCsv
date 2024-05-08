using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv.Reading;

internal readonly ref struct LineSeekArg<T> where T : unmanaged, IEquatable<T>
{
    public readonly ref readonly CsvReadingContext<T> Context;
    public readonly ref T[]? Array;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineSeekArg(
        ref readonly CsvReadingContext<T> context,
        ref T[]? array)
    {
        Debug.Assert(!Unsafe.IsNullRef(ref array));

        Context = ref context;
        Array = ref array;
    }

    public bool IsSingleTokenNewline
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Context.Dialect.Newline.Length == 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetRFC4188(out T quote, out T newline)
    {
        Debug.Assert(Context.Dialect.IsRFC4180Mode, "Invalid mode");
        Debug.Assert(Context.Dialect.Newline.Length == 1, $"Invalid newline length: {Context.Dialect.Newline.Length}");
        quote = Context.Dialect.Quote;
        newline = Context.Dialect.Newline.Span[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetRFC4188(out T quote, out T newline1, out T newline2)
    {
        Debug.Assert(Context.Dialect.IsRFC4180Mode, "Invalid mode");
        Debug.Assert(Context.Dialect.Newline.Length == 2, $"Invalid newline length: {Context.Dialect.Newline.Length}");

        quote = Context.Dialect.Quote;

        var span = Context.Dialect.Newline.Span;
        newline1 = span[0];
        newline2 = span[1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetUnix(out T quote, out T escape, out T newline)
    {
        Debug.Assert(!Context.Dialect.IsRFC4180Mode, "Invalid mode");
        Debug.Assert(Context.Dialect.Newline.Length == 1, $"Invalid newline length: {Context.Dialect.Newline.Length}");
        quote = Context.Dialect.Quote;
        escape = Context.Dialect.Escape.GetValueOrDefault();
        newline = Context.Dialect.Newline.Span[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetUnix(out T quote, out T escape, out T newline1, out T newline2)
    {
        Debug.Assert(!Context.Dialect.IsRFC4180Mode, "Invalid mode");
        Debug.Assert(Context.Dialect.Newline.Length == 2, $"Invalid newline length: {Context.Dialect.Newline.Length}");

        quote = Context.Dialect.Quote;
        escape = Context.Dialect.Escape.GetValueOrDefault();

        var span = Context.Dialect.Newline.Span;
        newline1 = span[0];
        newline2 = span[1];
    }
}

public sealed class CsvDataReader<T> where T : unmanaged, IEquatable<T>
{
    internal CsvSequenceReader<T> Reader;
    internal T[]? MultisegmentBuffer;

    internal int version;

    public void Reset(in ReadOnlySequence<T> sequence)
    {
        Debug.Assert(version == Reader.version, $"Version mismatch: {version} vs {Reader.version}");
        Reader = new(sequence) { version = ++version };
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0064:Make readonly fields writable", Justification = "<Pending>")]
internal struct CsvSequenceReader<T> where T : unmanaged, IEquatable<T>
{
    internal int version;

    private SequencePosition _currentPosition;
    private SequencePosition _nextPosition;
    private bool _moreData;
    private readonly long _length;

    /// <summary>
    /// Create a <see cref="SequenceReader{T}"/> over the given <see cref="ReadOnlySequence{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvSequenceReader(ReadOnlySequence<T> sequence)
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
    public int CurrentIndex { get; private set; }

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
    public long Consumed { get; private set; }

    /// <summary>
    /// Remaining <typeparamref name="T"/>'s in the reader's <see cref="Sequence"/>.
    /// </summary>
    public readonly long Remaining => Length - Consumed;

    /// <summary>
    /// Count of <typeparamref name="T"/> in the reader's <see cref="Sequence"/>.
    /// </summary>
    public readonly long Length
    {
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
        Debug.Assert(count >= 0);
        Debug.Assert(CurrentIndex + count <= Current.Length);

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
        CurrentIndex += (int)count;

        Debug.Assert(CurrentIndex < Current.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadLine(scoped LineSeekArg<T> arg, out ReadOnlyMemory<T> line, out RecordMeta meta)
    {
        Unsafe.SkipInit(out meta);

        if (arg.Context.Dialect.IsRFC4180Mode)
        {
            return arg.IsSingleTokenNewline
                ? TryReadLineSingle(arg, out line, out meta.quoteCount)  
                : TryReadLineMultiple(arg, out line, out meta.quoteCount);
        }
        else
        {
            return arg.IsSingleTokenNewline
                ? TryReadLineSingle(arg, out line, out meta.quoteCount, out meta.escapeCount)
                : TryReadLineMultiple(arg, out line, out meta.quoteCount, out meta.escapeCount);
        }
    }

    private bool TryReadLineSingle(scoped LineSeekArg<T> arg, out ReadOnlyMemory<T> line, out uint quoteCount)
    {
        CsvSequenceReader<T> copy = this;

        ReadOnlyMemory<T> unreadMemory = Unread;
        ReadOnlySpan<T> remaining = unreadMemory.Span;
        ref T lineStart = ref remaining.DangerousGetReference();
        quoteCount = 0;

        arg.GetRFC4188(out T quote, out T newline);

        while (!End)
        {
            Seek:
            int index = quoteCount % 2 == 0
                ? remaining.IndexOfAny(quote, newline)
                : remaining.IndexOf(quote);

            if (index != -1)
            {
                if (remaining[index].Equals(quote))
                {
                    quoteCount++;

                    if (AdvanceCurrent(index + 1))
                    {
                        remaining = Unread.Span;
                    }
                    else
                    {
                        remaining = remaining.Slice(index + 1);
                    }

                    goto Seek;
                }

                // Found one of the delimiters. Move to it, slice, then move past it.
                if (index > 0)
                {
                    AdvanceCurrent(index);
                }

                // perf: non-empty slice from the same segment, read directly from the original memory
                if (copy.Position.GetObject() == Position.GetObject() &&
                   (unreadMemory.Length | remaining.Length) != 0)
                {
                    int byteOffset = (int)Unsafe.ByteOffset(ref lineStart, ref remaining.DangerousGetReferenceAt(index));
                    int elementCount = byteOffset / Unsafe.SizeOf<T>();
                    line = unreadMemory.Slice(0, elementCount);

                    Debug.Assert(
                        Sequence.Slice(copy.Position, Position).SequenceEquals(line.Span),
                        $"Invalid slice: '{line}' vs '{Sequence.Slice(copy.Position, Position)}'");
                }
                else
                {
                    line = Sequence.Slice(copy.Position, Position).AsMemory(ref arg);
                }

                AdvanceCurrent(1);
                return true;
            }

            AdvanceCurrent(remaining.Length);
            remaining = Current.Span;
        }

        // Didn't find anything, reset our original state.
        this = copy;
        Unsafe.SkipInit(out line);
        return false;
    }

    private bool TryReadLineMultiple(scoped LineSeekArg<T> arg, out ReadOnlyMemory<T> line, out uint quoteCount)
    {
        CsvSequenceReader<T> copy = this;

        ReadOnlyMemory<T> unreadMemory = Unread;
        ReadOnlySpan<T> remaining = unreadMemory.Span;
        ref T lineStart = ref remaining.DangerousGetReference();
        quoteCount = 0;

        arg.GetRFC4188(out T quote, out T newline1, out T newline2);

        while (!End)
        {
            Seek:
            int index = quoteCount % 2 == 0
                ? remaining.IndexOfAny(quote, newline1)
                : remaining.IndexOf(quote);

            if (index != -1)
            {
                if (remaining[index].Equals(quote))
                {
                    quoteCount++;

                    if (AdvanceCurrent(index + 1))
                    {
                        remaining = Unread.Span;
                    }
                    else
                    {
                        remaining = remaining.Slice(index + 1);
                    }

                    goto Seek;
                }

                bool segmentHasChanged = false;

                // Found one of the delimiters
                if (AdvanceCurrent(index))
                    segmentHasChanged = true;

                // store first newline token position
                var crPosition = Position;

                // and advance past it
                if (AdvanceCurrent(1))
                    segmentHasChanged = true;

                if (IsNext(newline2, advancePast: false))
                {
                    // perf: non-empty slice from the same segment, read directly from the original memory
                    if (copy.Position.GetObject() == crPosition.GetObject() &&
                        (unreadMemory.Length | remaining.Length) != 0)
                    {
                        int byteOffset = (int)Unsafe.ByteOffset(ref lineStart, ref remaining.DangerousGetReferenceAt(index));
                        int elementCount = byteOffset / Unsafe.SizeOf<T>();
                        line = unreadMemory.Slice(0, elementCount);

                        Debug.Assert(
                            Sequence.Slice(copy.Position, crPosition).SequenceEquals(line.Span),
                            $"Invalid slice: '{line}' vs '{Sequence.Slice(copy.Position, crPosition)}'");
                    }
                    else
                    {
                        line = Sequence.Slice(copy.Position, crPosition).AsMemory(ref arg);
                    }

                    // advance past second newline token
                    AdvanceCurrent(1);
                    return true;
                }

                if (!TryAdvance(1, out bool segmentChanged))
                    break;

                if (segmentChanged || segmentHasChanged)
                    remaining = Unread.Span;

                goto Seek;
            }

            AdvanceCurrent(remaining.Length);
            remaining = Current.Span;
        }

        // Didn't find anything, reset our original state.
        this = copy;
        Unsafe.SkipInit(out line);
        return false;
    }

    public bool TryReadLine(scoped LineSeekArg<T> arg, out ReadOnlyMemory<T> line, out uint quoteCount, out uint escapeCount)
    {
        Debug.Assert(!arg.Context.Dialect.IsRFC4180Mode);
        return arg.IsSingleTokenNewline
            ? TryReadLineSingle(arg, out line, out quoteCount, out escapeCount)
            : TryReadLineMultiple(arg, out line, out quoteCount, out escapeCount);
    }

    private bool TryReadLineSingle(scoped LineSeekArg<T> arg, out ReadOnlyMemory<T> line, out uint quoteCount, out uint escapeCount)
    {
        CsvSequenceReader<T> copy = this;

        ReadOnlyMemory<T> unreadMemory = Unread;
        ReadOnlySpan<T> remaining = unreadMemory.Span;
        ref T lineStart = ref remaining.DangerousGetReference();
        quoteCount = 0;
        escapeCount = 0;

        arg.GetUnix(out T quote, out T escape, out T newline);

        while (!End)
        {
            Seek:
            int index = quoteCount % 2 == 0
                ? remaining.IndexOfAny(quote, escape, newline)
                : remaining.IndexOfAny(quote, escape);

            if (index != -1)
            {
                if (remaining[index].Equals(escape))
                {
                    escapeCount++;

                    if (!TryAdvance(index + 2, out bool refreshRemaining))
                        break;

                    if (refreshRemaining)
                    {
                        remaining = Unread.Span;
                    }
                    else
                    {
                        remaining = remaining.Slice(index + 2);
                    }

                    goto Seek;
                }

                if (remaining[index].Equals(quote))
                {
                    quoteCount++;

                    if (AdvanceCurrent(index + 1))
                    {
                        remaining = Unread.Span;
                    }
                    else
                    {
                        remaining = remaining.Slice(index + 1);
                    }

                    goto Seek;
                }

                // Found one of the delimiters. Move to it, slice, then move past it.
                if (index > 0)
                {
                    AdvanceCurrent(index);
                }

                // perf: non-empty slice from the same segment, read directly from the original memory
                if (copy.Position.GetObject() == Position.GetObject() &&
                   (unreadMemory.Length | remaining.Length) != 0)
                {
                    int byteOffset = (int)Unsafe.ByteOffset(ref lineStart, ref remaining.DangerousGetReferenceAt(index));
                    int elementCount = byteOffset / Unsafe.SizeOf<T>();
                    line = unreadMemory.Slice(0, elementCount);

                    Debug.Assert(
                        Sequence.Slice(copy.Position, Position).SequenceEquals(line.Span),
                        $"Invalid slice: '{line}' vs '{Sequence.Slice(copy.Position, Position)}'");
                }
                else
                {
                    line = Sequence.Slice(copy.Position, Position).AsMemory(ref arg);
                }

                AdvanceCurrent(1);
                return true;
            }

            AdvanceCurrent(remaining.Length);
            remaining = Current.Span;
        }

        // Didn't find anything, reset our original state.
        this = copy;
        Unsafe.SkipInit(out line);
        return false;
    }

    private bool TryReadLineMultiple(scoped LineSeekArg<T> arg, out ReadOnlyMemory<T> line, out uint quoteCount, out uint escapeCount)
    {
        CsvSequenceReader<T> copy = this;

        ReadOnlyMemory<T> unreadMemory = Unread;
        ReadOnlySpan<T> remaining = unreadMemory.Span;
        ref T lineStart = ref remaining.DangerousGetReference();
        quoteCount = 0;
        escapeCount = 0;

        arg.GetUnix(out T quote, out T escape, out T newline1, out T newline2);

        while (!End)
        {
            Seek:
            int index = quoteCount % 2 == 0
                ? remaining.IndexOfAny(quote, escape, newline1)
                : remaining.IndexOfAny(quote, escape);

            if (index != -1)
            {
#if DEBUG
                T token = remaining[index];
#endif

                if (remaining[index].Equals(escape))
                {
                    escapeCount++;

                    if (!TryAdvance(index + 2, out bool refreshRemaining))
                        break;

                    if (refreshRemaining)
                    {
                        remaining = Unread.Span;
                    }
                    else
                    {
                        remaining = remaining.Slice(index + 2);
                    }

                    goto Seek;
                }

                if (remaining[index].Equals(quote))
                {
                    quoteCount++;

                    if (AdvanceCurrent(index + 1))
                    {
                        remaining = Unread.Span;
                    }
                    else
                    {
                        remaining = remaining.Slice(index + 1);
                    }

                    goto Seek;
                }

                bool segmentHasChanged = false;

                // Found one of the delimiters
                if (AdvanceCurrent(index))
                    segmentHasChanged = true;

                // store first newline token position
                var crPosition = Position;

                // and advance past it
                if (AdvanceCurrent(1))
                    segmentHasChanged = true;

                if (IsNext(newline2, advancePast: false))
                {
                    // perf: non-empty slice from the same segment, read directly from the original memory
                    if (copy.Position.GetObject() == crPosition.GetObject() &&
                        (unreadMemory.Length | remaining.Length) != 0)
                    {
                        int byteOffset = (int)Unsafe.ByteOffset(ref lineStart, ref remaining.DangerousGetReferenceAt(index));
                        int elementCount = byteOffset / Unsafe.SizeOf<T>();
                        line = unreadMemory.Slice(0, elementCount);

                        Debug.Assert(
                            Sequence.Slice(copy.Position, crPosition).SequenceEquals(line.Span),
                            $"Invalid slice: '{line}' vs '{Sequence.Slice(copy.Position, crPosition)}'");
                    }
                    else
                    {
                        line = Sequence.Slice(copy.Position, crPosition).AsMemory(ref arg);
                    }

                    // advance past second newline token
                    AdvanceCurrent(1);
                    return true;
                }

                if (!TryAdvance(1, out bool segmentChanged))
                    break;

                if (segmentChanged || segmentHasChanged)
                    remaining = Unread.Span;

                goto Seek;
            }

            AdvanceCurrent(remaining.Length);
            remaining = Current.Span;
        }

        // Didn't find anything, reset our original state.
        this = copy;
        Unsafe.SkipInit(out line);
        return false;
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
