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
    public static ReadOnlySequence<T> AsSequence(
        ReadOnlyMemory<T> data,
        int bufferSize,
        int emptyFrequency = 0)
    {
        if (bufferSize == -1 || data.Length <= bufferSize)
        {
            return new ReadOnlySequence<T>(data);
        }

        MemorySegment<T> first = new(data.Slice(0, bufferSize));
        MemorySegment<T> last = first;

        ReadOnlyMemory<T> remaining = data.Slice(bufferSize);

        int counter = 0;

        while (remaining.Length > bufferSize)
        {
            last = last.Append(remaining.Slice(0, bufferSize));
            remaining = remaining.Slice(bufferSize);

            if (emptyFrequency > 0 && ++counter % emptyFrequency == 0)
            {
                last = last.Append(ReadOnlyMemory<T>.Empty);
            }
        }

        last = last.Append(remaining);
        return new(first, 0, last, last.Memory.Length);
    }
}
