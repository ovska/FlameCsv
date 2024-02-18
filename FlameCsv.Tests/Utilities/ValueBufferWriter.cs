using System.Buffers;

namespace FlameCsv.Tests.Utilities;

internal readonly struct ValueBufferWriter<T> : IBufferWriter<T>
{
    public ValueBufferWriter()
    {
    }

    public readonly ArrayBufferWriter<T> Writer { get; } = new();

    public void Advance(int count) => Writer.Advance(count);
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        var memory = Writer.GetMemory(sizeHint);

        if (memory.Length > sizeHint)
            memory = memory.Slice(0, sizeHint);

        return memory;
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        return GetMemory(sizeHint).Span;
    }
}
