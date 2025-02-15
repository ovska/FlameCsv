using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail.
/// </summary>
/// <param name="buffer">Read data</param>
/// <param name="isCompleted">Whether any more data can be read from the reader after this</param>
[DebuggerDisplay(@"\{ ReadResult<{typeof(T).Name,nq}> Length: {Buffer.Length}, IsCompleted: {IsCompleted} \}")]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public readonly struct CsvReadResult<T>(in ReadOnlySequence<T> buffer, bool isCompleted)
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Data read from the data source, e.g., PipeReader or TextReader.
    /// </summary>
    public ReadOnlySequence<T> Buffer { get; } = buffer;

    /// <summary>
    /// If true, no more data can be read from the data source and all further reads will return an empty buffer.
    /// </summary>
    public bool IsCompleted { get; } = isCompleted;

    /// <summary>
    /// Deconstructs the result into its components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out ReadOnlySequence<T> buffer, out bool isCompleted)
    {
        buffer = Buffer;
        isCompleted = IsCompleted;
    }
}
