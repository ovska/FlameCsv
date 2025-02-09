using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface ICsvPipeReader<T> : IDisposable, IAsyncDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Reads the next block of data from the data source.
    /// </summary>
    ValueTask<CsvReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the reader to the specified position.
    /// </summary>
    /// <param name="consumed">End position of the consumed data from the last read.</param>
    /// <param name="examined">End position of the examined data from the last read.</param>
    void AdvanceTo(SequencePosition consumed, SequencePosition examined);
}

/// <summary>
/// Internal implementation detail.
/// </summary>
/// <param name="buffer">Read data</param>
/// <param name="isCompleted">Whether any more data can be read from the reader after this</param>
[DebuggerDisplay(@"\{ ReadResult<{typeof(T).Name,nq}> Length: {Buffer.Length}, IsCompleted: {IsCompleted} \}")]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
[EditorBrowsable(EditorBrowsableState.Advanced)]
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
