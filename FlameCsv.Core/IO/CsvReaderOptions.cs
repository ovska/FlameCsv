using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Represents options for configuring <see cref="ICsvBufferReader{T}"/>.
/// </summary>
/// <seealso cref="CsvBufferReader"/>
[PublicAPI]
public readonly struct CsvReaderOptions
{
    /// <summary>
    /// The default buffer size.
    /// </summary>
    public const int DefaultBufferSize = 1024 * 16;

    /// <summary>
    /// The default minimum read size.
    /// </summary>
    public const int DefaultMinimumReadSize = 1024;

    /// <summary>
    /// The minimum buffer and read size.
    /// </summary>
    public const int MinimumBufferSize = 256;

    private readonly int? _bufferSize;
    private readonly int? _minimumReadSize;

    /// <summary>
    /// Gets or sets the buffer size. If set to -1, the default buffer size is used.<br/>
    /// This value will be clamped to be at minimum <see cref="MinimumReadSize"/> and <see cref="MinimumBufferSize"/>.
    /// </summary>
    /// <remarks>
    /// This is only the initial value; the buffer may be resized during reading if needed.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the value is negative and not -1.
    /// </exception>
    public int BufferSize
    {
        get => Math.Max(_bufferSize ?? DefaultBufferSize, MinimumReadSize);
        init
        {
            if (value == -1) return;
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _bufferSize = Math.Max(value, MinimumBufferSize);
        }
    }

    /// <summary>
    /// Gets or sets the minimum read size. If unset or set to -1, the default minimum read size is used.<br/>
    /// This value will be clamped to be at minimum <see cref="MinimumBufferSize"/> / 2.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the value is less than 1 and not equal to -1.
    /// </exception>
    public int MinimumReadSize
    {
        get => _minimumReadSize ?? DefaultMinimumReadSize;
        init
        {
            if (value == -1) return;
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _minimumReadSize = Math.Max(value, MinimumBufferSize / 2);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the inner stream/reader should be left open after reading.<br/>
    /// The default is <c>false</c>.
    /// </summary>
    public bool LeaveOpen { get; init; }

    /// <summary>
    /// Disable direct buffer reading optimization when reading <see cref="MemoryStream"/> or
    /// <see cref="StringReader"/> with an exposable buffer.<br/>
    /// The default is <c>false</c>.
    /// </summary>
    public bool NoDirectBufferAccess { get; init; }
}
