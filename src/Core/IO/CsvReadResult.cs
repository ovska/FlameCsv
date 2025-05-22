using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Represents the result of a read operation from a <see cref="ICsvBufferReader{T}"/>.
/// </summary>
/// <param name="buffer">Read data</param>
/// <param name="isCompleted">Whether any more data can be read from the reader</param>
[PublicAPI]
[DebuggerDisplay(@"\{ ReadResult<{typeof(T).Name,nq}> Length: {Buffer.Length}, IsCompleted: {IsCompleted} \}")]
[EditorBrowsable(EditorBrowsableState.Advanced)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct CsvReadResult<T>(ReadOnlyMemory<T> buffer, bool isCompleted)
    where T : unmanaged
{
    /// <summary>
    /// Returns a read result with an empty buffer and <see cref="IsCompleted"/> set to <c>false</c>.
    /// </summary>
    public static readonly CsvReadResult<T> Empty;

    /// <summary>
    /// Returns a read result with an empty buffer and <see cref="IsCompleted"/> set to <c>true</c>.
    /// </summary>
    public static CsvReadResult<T> Completed => new(default, true);

    /// <summary>
    /// Unprocessed data read from the data source.
    /// </summary>
    public ReadOnlyMemory<T> Buffer => buffer;

    /// <summary>
    /// If <c>true</c>, no more data can be read from the data source.
    /// </summary>
    public bool IsCompleted => isCompleted;

    /// <summary>
    /// Deconstructs the result into its components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out ReadOnlyMemory<T> buffer, out bool isCompleted)
    {
        buffer = Buffer;
        isCompleted = IsCompleted;
    }
}
