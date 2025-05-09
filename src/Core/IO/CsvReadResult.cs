using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Internal implementation detail.
/// </summary>
[PublicAPI]
[DebuggerDisplay(@"\{ ReadResult<{typeof(T).Name,nq}> Length: {Buffer.Length}, IsCompleted: {IsCompleted} \}")]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public readonly struct CsvReadResult<T>
    where T : unmanaged
{
    /// <summary>
    /// Internal implementation detail.
    /// </summary>
    /// <param name="buffer">Read data</param>
    /// <param name="isCompleted">Whether any more data can be read from the reader after this</param>
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvReadResult(ReadOnlyMemory<T> buffer, bool isCompleted)
    {
        Buffer = buffer;
        IsCompleted = isCompleted;
    }

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
    public ReadOnlyMemory<T> Buffer { get; }

    /// <summary>
    /// If <c>true</c>, no more data can be read from the data source.
    /// </summary>
    public bool IsCompleted { get; }

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
