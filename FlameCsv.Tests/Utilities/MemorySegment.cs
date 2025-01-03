using System.Buffers;

namespace FlameCsv.Tests.Utilities;

internal class MemorySegment<T> : ReadOnlySequenceSegment<T>
{
    public MemorySegment(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }

    public MemorySegment<T> Append(ReadOnlyMemory<T> memory)
    {
        var segment = new MemorySegment<T>(memory)
        {
            RunningIndex = RunningIndex + Memory.Length,
        };

        Next = segment;
        return segment;
    }

    /// <summary>
    /// Buffers the parameter data into segments of specified max length.
    /// </summary>
    /// <param name="data">Source data</param>
    /// <param name="bufferSize">Segment max length, -1 to always return a single segment</param>
    /// <param name="emptyFrequency">How often should an empty segment be inserted</param>
    /// <param name="factory">Optional factory to create the memory instances</param>
    public static ReadOnlySequence<T> AsSequence(
        ReadOnlyMemory<T> data,
        int bufferSize,
        int emptyFrequency = 0,
        Func<ReadOnlyMemory<T>, ReadOnlyMemory<T>>? factory = null)
    {
        factory ??= m => m;

        if (bufferSize == -1 || data.Length <= bufferSize)
        {
            return new ReadOnlySequence<T>(factory(data));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        MemorySegment<T> first = new(factory(data.Slice(0, bufferSize)));
        MemorySegment<T> last = first;

        ReadOnlyMemory<T> remaining = data.Slice(bufferSize);

        int counter = 0;

        while (remaining.Length > bufferSize)
        {
            last = last.Append(factory(remaining.Slice(0, bufferSize)));
            remaining = remaining.Slice(bufferSize);

            if (emptyFrequency > 0 && ++counter % emptyFrequency == 0)
            {
                last = last.Append(ReadOnlyMemory<T>.Empty);
            }
        }

        last = last.Append(factory(remaining));
        return new(first, 0, last, last.Memory.Length);
    }
}
