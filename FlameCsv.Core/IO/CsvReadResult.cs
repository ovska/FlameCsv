using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Internal implementation detail.
/// </summary>
/// <param name="buffer">Read data</param>
/// <param name="isCompleted">Whether any more data can be read from the reader after this</param>
[PublicAPI]
[DebuggerDisplay(@"\{ ReadResult<{typeof(T).Name,nq}> Length: {Buffer.Length}, IsCompleted: {IsCompleted} \}")]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public readonly struct CsvReadResult<T>(in ReadOnlySequence<T> buffer, bool isCompleted)
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// A read result with an empty buffer and <see cref="IsCompleted"/> set to <see langword="false"/>.
    /// </summary>
    public static readonly CsvReadResult<T> Empty;

    /// <summary>
    /// Data read from the data source, e.g., PipeReader or TextReader.
    /// </summary>
    public readonly ReadOnlySequence<T> Buffer = buffer;

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
