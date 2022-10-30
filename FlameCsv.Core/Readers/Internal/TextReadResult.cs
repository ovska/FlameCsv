using System.Buffers;
using System.Diagnostics;

namespace FlameCsv.Readers.Internal;

#if DEBUG
[DebuggerDisplay(
    @"\{ ReadResult, Buffer: {Buffer.Length}, IsCompleted: {IsCompleted} \}",
    Target = typeof(System.IO.Pipelines.ReadResult))]
#endif

[DebuggerDisplay(@"\{ TextReadResult, Buffer: {Buffer.Length}, IsCompleted: {IsCompleted} \}")]
internal readonly struct TextReadResult
{
    /// <summary>
    /// Buffer containing the read data. May be empty.
    /// </summary>
    public ReadOnlySequence<char> Buffer { get; }

    /// <summary>
    /// There is no more data to read.
    /// </summary>
    public bool IsCompleted { get; }

    public TextReadResult(ReadOnlySequence<char> buffer, bool isCompleted)
    {
        Buffer = buffer;
        IsCompleted = isCompleted;
    }
}
