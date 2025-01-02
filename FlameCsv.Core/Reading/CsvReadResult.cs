using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

[DebuggerDisplay(@"\{ ReadResult<{typeof(T).Name,nq}, Buffer Length: {Buffer.Length}, IsCompleted: {IsCompleted} \}")]
internal readonly struct CsvReadResult<T> where T : unmanaged, IEquatable<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvReadResult(in ReadOnlySequence<T> buffer, bool isCompleted)
    {
        Buffer = buffer;
        IsCompleted = isCompleted;
    }

    /// <summary>
    /// Data read from the data source, e.g. PipeReader or TextReader. May be empty.
    /// </summary>
    public ReadOnlySequence<T> Buffer { get; }

    /// <summary>
    /// If true, no more data can be read from the data source and all subsequent reads will return an empty buffer.
    /// </summary>
    public bool IsCompleted { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out ReadOnlySequence<T> buffer, out bool isCompleted)
    {
        buffer = Buffer;
        isCompleted = IsCompleted;
    }
}
