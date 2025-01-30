using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

[EditorBrowsable(EditorBrowsableState.Never)]
[DebuggerDisplay(@"\{ ReadResult<{typeof(T).Name,nq}> Length: {Buffer.Length}, IsCompleted: {IsCompleted} \}")]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct CsvReadResult<T>(in ReadOnlySequence<T> buffer, bool isCompleted) where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Data read from the data source, e.g., PipeReader or TextReader.
    /// </summary>
    public ReadOnlySequence<T> Buffer { get; } = buffer;

    /// <summary>
    /// If true, no more data can be read from the data source and all further reads will return an empty buffer.
    /// </summary>
    public bool IsCompleted { get; } = isCompleted;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out ReadOnlySequence<T> buffer, out bool isCompleted)
    {
        buffer = Buffer;
        isCompleted = IsCompleted;
    }
}
