using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace FlameCsv.Utilities;

internal sealed class StringBuilderSegment : ReadOnlySequenceSegment<char>
{
    public static ReadOnlySequence<char> Create(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var enumerator = builder.GetChunks();

        if (!enumerator.MoveNext())
            return default;

        ReadOnlyMemory<char> firstMemory = enumerator.Current;

        // optimization: use the memory directly for single-segment strings
        if (!enumerator.MoveNext())
            return new ReadOnlySequence<char>(firstMemory);

        StringBuilderSegment first = new(firstMemory);
        StringBuilderSegment last = first;

        do
        {
            last = last.Append(enumerator.Current);
        } while (enumerator.MoveNext());

        Debug.Assert((last.Memory.Length + last.RunningIndex) == builder.Length);

        return new(first, 0, last, last.Memory.Length);
    }

    private StringBuilderSegment(ReadOnlyMemory<char> memory)
    {
        Memory = memory;
    }

    private StringBuilderSegment Append(ReadOnlyMemory<char> memory)
    {
        var segment = new StringBuilderSegment(memory) { RunningIndex = RunningIndex + Memory.Length };
        Next = segment;
        return segment;
    }
}
