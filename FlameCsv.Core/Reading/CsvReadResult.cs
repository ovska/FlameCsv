using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

[DebuggerDisplay(@"\{ ReadResult<{typeof(T).Name,nq}, Buffer Length: {Buffer.Length}, IsCompleted: {IsCompleted} \}")]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct CsvReadResult<T>(ReadOnlySequence<T> buffer, bool isCompleted) where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Data read from the data source, e.g. PipeReader or TextReader. May be empty.
    /// </summary>
    public ReadOnlySequence<T> Buffer => buffer;

    /// <summary>
    /// If true, no more data can be read from the data source and all subsequent reads will return an empty buffer.
    /// </summary>
    public bool IsCompleted => isCompleted;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out ReadOnlySequence<T> buffer, out bool isCompleted)
    {
        buffer = Buffer;
        isCompleted = IsCompleted;
    }
}
