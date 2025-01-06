using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

// based HEAVILY on the .NET runtime BufferSegment code
[DebuggerDisplay(
    @"\{ TextSegment, Memory Length: {AvailableMemory.Length}, Index: {RunningIndex}, IsLast: {_next == null} \}")]
internal sealed class TextSegment(MemoryPool<char> allocator) : ReadOnlySequenceSegment<char>
{
    internal IMemoryOwner<char>? _memory;
    private TextSegment? _next;
    private int _end;

    /// <summary>
    /// The End represents the offset into AvailableMemory where the range of "active" bytes ends. At the point when the block is leased
    /// the End is guaranteed to be equal to Start. The value of Start may be assigned anywhere between 0 and
    /// Buffer.Length, and must be equal to or less than End.
    /// </summary>
    public int End
    {
        get => _end;
        set
        {
            Debug.Assert(value <= AvailableMemory.Length);

            _end = value;
            Memory = AvailableMemory.Slice(0, value);
        }
    }

    /// <summary>
    /// Reference to the next block of data when the overall "active" bytes spans multiple blocks. At the point when the block is
    /// leased Next is guaranteed to be null. Start, End, and Next are used together in order to create a linked-list of discontiguous
    /// working memory. The "active" memory is grown when bytes are copied in, End is increased, and Next is assigned. The "active"
    /// memory is shrunk when bytes are consumed, Start is increased, and blocks ar e returned to the pool.
    /// </summary>
    public TextSegment? NextSegment
    {
        get => _next;
        set
        {
            Next = value;
            _next = value;
        }
    }

    public void SetOwnedMemory(int bufferSize)
    {
        AvailableMemory = (_memory = allocator.Rent(bufferSize)).Memory;
    }

    public void ResetMemory()
    {
        AvailableMemory = default;
        _memory?.Dispose();
        _memory = null!;

        Next = null;
        RunningIndex = 0;
        Memory = default;
        _next = null;
        _end = 0;
    }

    public Memory<char> AvailableMemory { get; private set; }

    public int Length => End;

    public int WritableBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => AvailableMemory.Length - End;
    }

    public void SetNext(TextSegment segment)
    {
        Debug.Assert(segment is not null);
        Debug.Assert(Next is null);

        NextSegment = segment;

        segment = this;

        while (segment.Next != null)
        {
            Debug.Assert(segment.NextSegment is not null);
            segment.NextSegment.RunningIndex = segment.RunningIndex + segment.Length;
            segment = segment.NextSegment;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long GetLength(TextSegment startSegment, int startIndex, TextSegment endSegment, int endIndex)
    {
        return endSegment.RunningIndex + (uint)endIndex - (startSegment.RunningIndex + (uint)startIndex);
    }
}
