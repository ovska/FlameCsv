using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Represents the result of a read operation from a <see cref="ICsvBufferReader{T}"/>.
/// </summary>
/// <param name="buffer">Read data</param>
/// <param name="isCompleted">Whether any more data can be read from the reader</param>
[PublicAPI]
[DebuggerDisplay(@"{ToString(),nq}")]
[EditorBrowsable(EditorBrowsableState.Advanced)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct CsvReadResult<T>(ReadOnlyMemory<T> buffer, bool isCompleted)
    where T : unmanaged
{
    /// <summary>
    /// Returns a read result with an empty buffer and <see cref="IsCompleted"/> set to <c>true</c>.
    /// </summary>
    public static CsvReadResult<T> Completed => new(default, true);

    /// <summary>
    /// Unprocessed data read from the data source.
    /// </summary>
    public ReadOnlyMemory<T> Buffer { get; } = buffer;

    /// <summary>
    /// If <c>true</c>, no more data can be read from the data source.
    /// </summary>
    public bool IsCompleted { get; } = isCompleted;

    /// <summary>
    /// Deconstructs the result into its components.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out ReadOnlyMemory<T> buffer, out bool isCompleted)
    {
        buffer = Buffer;
        isCompleted = IsCompleted;
    }

    /// <inheritdoc/>
    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        return $"ReadResult<{Token<T>.Name}> Length: {Buffer.Length}, IsCompleted: {IsCompleted}";
    }
}
