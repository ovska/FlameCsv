using System.Buffers;
using System.ComponentModel;
using FlameCsv.Extensions;
using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Represents options for configuring <see cref="ICsvPipeReader{T}"/>.
/// </summary>
/// <seealso cref="CsvPipeReader"/>
[PublicAPI]
public readonly struct CsvReaderOptions
{
    /// <summary>
    /// The default buffer size in T.
    /// </summary>
    public const int DefaultBufferSize = 4096;

    /// <summary>
    /// The default minimum read size in T.
    /// </summary>
    public const int DefaultMinimumReadSize = 1024;

    private readonly int? _bufferSize;
    private readonly int? _minimumReadSize;

    /// <summary>
    /// Gets or sets the buffer size in T. If set to -1, the default buffer size is used.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the value is less than 1 and not equal to -1.</exception>
    public int BufferSize
    {
        get => Math.Max(_bufferSize ?? DefaultBufferSize, MinimumReadSize);
        init
        {
            if (value != -1) ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _bufferSize = value == -1 ? DefaultBufferSize : value;
        }
    }

    /// <summary>
    /// Gets or sets the minimum read size in T. If set to -1, the default minimum read size is used.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the value is less than 1 and not equal to -1.</exception>
    public int MinimumReadSize
    {
        get => _minimumReadSize ?? DefaultMinimumReadSize;
        init
        {
            if (value != -1) ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _minimumReadSize = value == -1 ? DefaultMinimumReadSize : value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the inner stream/reader should be left open after reading.
    /// </summary>
    public bool LeaveOpen { get; init; }

    /// <summary>
    /// Disable direct buffer reading optimization when reading <see cref="MemoryStream"/> or
    /// <see cref="StringReader"/> with an exposable buffer.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool NoDirectBufferAccess { get; init; }

    /// <summary>
    /// Ensures the configured buffer sizes are valid for the specified memory pool.
    /// </summary>
    /// <param name="memoryPool">Pool instance</param>
    public void EnsureValid<T>(MemoryPool<T> memoryPool)
    {
        ArgumentNullException.ThrowIfNull(memoryPool);

        Throw.IfInvalidArgument(
            BufferSize > memoryPool.MaxBufferSize,
            "The default buffer size is too large for the memory pool",
            nameof(MinimumReadSize));

        Throw.IfInvalidArgument(
            MinimumReadSize > memoryPool.MaxBufferSize,
            "The minimum read size is too large for the memory pool",
            nameof(MinimumReadSize));
    }
}
