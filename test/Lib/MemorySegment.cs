using System.Buffers;
using FlameCsv.IO;

namespace FlameCsv.Tests;

public static class MemorySegment
{
    public static ReadOnlySequence<char> Create(params ReadOnlySpan<string?> segments)
    {
        if (segments.IsEmpty)
            return ReadOnlySequence<char>.Empty;

        MemorySegment<char> first = new(segments[0].AsMemory());
        MemorySegment<char> last = first;

        for (int i = 1; i < segments.Length; i++)
        {
            last = last.Append(segments[i].AsMemory());
        }

        return new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
    }

    public static ReadOnlySequence<char> Create(params ReadOnlySpan<ReadOnlyMemory<char>> segments)
    {
        if (segments.IsEmpty)
            return ReadOnlySequence<char>.Empty;

        MemorySegment<char> first = new(segments[0]);
        MemorySegment<char> last = first;

        for (int i = 1; i < segments.Length; i++)
        {
            last = last.Append(segments[i]);
        }

        return new ReadOnlySequence<char>(first, 0, last, last.Memory.Length);
    }
}

public class MemorySegment<T> : ReadOnlySequenceSegment<T>
    where T : unmanaged
{
    public MemorySegment(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }

    public MemorySegment<T> Append(ReadOnlyMemory<T> memory)
    {
        var segment = new MemorySegment<T>(memory) { RunningIndex = RunningIndex + Memory.Length };

        Next = segment;
        return segment;
    }

    /// <summary>
    /// Buffers the parameter data into segments of specified max length.
    /// </summary>
    /// <param name="data">Source data</param>
    /// <param name="bufferSize">Segment max length, -1 to always return a single segment</param>
    /// <param name="emptyFrequency">How often should an empty segment be inserted</param>
    public static ReadOnlySequence<T> AsSequence(ReadOnlyMemory<T> data, int bufferSize, int emptyFrequency = 0)
    {
        if (bufferSize == -1 || data.Length <= bufferSize)
        {
            return new ReadOnlySequence<T>(data);
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

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

    public static IDisposable Create(
        ReadOnlyMemory<T> data,
        int bufferSize,
        int emptyFrequency,
        IBufferPool? pool,
        out ReadOnlySequence<T> sequence
    )
    {
        DisposableCollection owners = new();

        if (bufferSize == -1 || data.Length <= bufferSize)
        {
            sequence = new ReadOnlySequence<T>(GetMemory(data));
            return owners;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        MemorySegment<T> first = new(GetMemory(data.Slice(0, bufferSize)));
        MemorySegment<T> last = first;

        ReadOnlyMemory<T> remaining = data.Slice(bufferSize);

        int counter = 0;

        while (remaining.Length > bufferSize)
        {
            last = last.Append(GetMemory(remaining.Slice(0, bufferSize)));
            remaining = remaining.Slice(bufferSize);

            if (emptyFrequency > 0 && ++counter % emptyFrequency == 0)
            {
                last = last.Append(ReadOnlyMemory<T>.Empty);
            }
        }

        last = last.Append(GetMemory(remaining));
        sequence = new(first, 0, last, last.Memory.Length);
        return owners;

        ReadOnlyMemory<T> GetMemory(ReadOnlyMemory<T> memory)
        {
            if (pool is null)
                return memory;

            var owner = pool.Rent<T>(memory.Length);
            memory.CopyTo(owner.Memory);
            owners.Add(owner);
            return owner.Memory.Slice(0, memory.Length);
        }
    }
}
