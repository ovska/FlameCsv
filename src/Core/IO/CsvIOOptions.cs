using JetBrains.Annotations;

namespace FlameCsv.IO;

/// <summary>
/// Represents options for configuring <see cref="ICsvBufferReader{T}"/> and <see cref="ICsvBufferWriter{T}"/>.
/// </summary>
/// <seealso cref="CsvBufferReader"/>
[PublicAPI]
public readonly record struct CsvIOOptions
{
    /// <summary>
    /// The default buffer size (16 KiB).
    /// </summary>
    /// <seealso cref="BufferSize"/>
    public const int DefaultBufferSize = 1024 * 16;

    /// <summary>
    /// The default buffer size when doing file I/O (64 KiB).<br/>
    /// This is used with the file I/O methods in <see cref="Csv"/>.
    /// if <see cref="BufferSize"/> is not explicitly configured.
    /// </summary>
    /// <seealso cref="BufferSize"/>
    public const int DefaultFileBufferSize = 1024 * 64;

    /// <summary>
    /// The default minimum read size (1 KiB).<br/>
    /// This is used when the minimum read size is not explicitly configured.
    /// </summary>
    /// <seealso cref="MinimumReadSize"/>
    public const int DefaultMinimumReadSize = 1024;

    /// <summary>
    /// The minimum buffer size, values below this will be clamped.
    /// </summary>
    public const int MinimumBufferSize = 256;

    private readonly int? _bufferSize;
    private readonly int? _minimumReadSize;

    /// <summary>
    /// Gets or sets the buffer size to use when working with streaming I/O. This value may be ignored when reading
    /// constant data such as <see cref="ReadOnlyMemory{T}"/>.<br/>
    /// If set to -1, the default buffer size is used.<br/>
    /// This value will be clamped to be at minimum <see cref="MinimumBufferSize"/>.
    /// </summary>
    /// <remarks>
    /// This is only the initial value; the buffer may be resized during reading if needed.<br/>
    /// If unset, either <see cref="DefaultBufferSize"/> or <see cref="DefaultFileBufferSize"/> will be used
    /// depending on the context.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the value is negative or zero and not -1.
    /// </exception>
    /// <seealso cref="DefaultBufferSize"/>
    /// <seealso cref="DefaultFileBufferSize"/>
    public int BufferSize
    {
        get => _bufferSize ?? DefaultBufferSize;
        init
        {
            if (value != -1)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
                _bufferSize = Math.Max(value, MinimumBufferSize);
            }
        }
    }

    /// <summary>
    /// Gets or sets the minimum available data size when reading from streaming sources. This value may be ignored when reading
    /// constant data such as <see cref="ReadOnlyMemory{T}"/>.<br/>
    /// If unset or set to -1, the smaller of <see cref="DefaultMinimumReadSize"/>
    /// and <see cref="BufferSize"/> / 2 will be used.
    /// </summary>
    /// <remarks>
    /// This threshold determines when the next read operation is performed to fill the buffer.
    /// It should generally be set to a large enough value that it fits at least a single complete CSV record,
    /// but small enough compared to <see cref="BufferSize"/> to not needlessly perform I/O if there are unread records.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the value is negative or zero and not -1.
    /// </exception>
    /// <seealso cref="DefaultMinimumReadSize"/>
    public int MinimumReadSize
    {
        get
        {
            int minReadSize = _minimumReadSize ?? DefaultMinimumReadSize;
            int bufferSize = _bufferSize ?? DefaultBufferSize;
            return Math.Min(minReadSize, bufferSize / 2);
        }
        init
        {
            if (value != -1)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
                _minimumReadSize = Math.Max(value, MinimumBufferSize / 2);
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the inner stream/reader should be left open after use.<br/>
    /// The default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// This parameter is ignored if the stream was not created by the library, such as when writing to a file.
    /// </remarks>
    /// <seealso cref="ForFileIO"/>
    public bool LeaveOpen { get; init; }

    /// <summary>
    /// Disable direct buffer reading optimization when reading <see cref="MemoryStream"/> or
    /// <see cref="StringReader"/> with an exposable buffer.<br/>
    /// The default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// An internal optimization reads directly from the internal buffer from a set of known types.
    /// If this behavior is undesired, set this to <c>true</c>.
    /// </remarks>
    public bool NoDirectBufferAccess { get; init; }

    /// <summary>
    /// Gets or sets the buffer pool used for renting buffers.<br/>
    /// The default is <c>null</c>, which uses <see cref="System.Buffers.MemoryPool{T}.Shared"/>.
    /// </summary>
    public IBufferPool? BufferPool { get; init; }

    internal IBufferPool EffectiveBufferPool => BufferPool ?? DefaultBufferPool.Instance;

    /// <summary>
    /// Returns <c>true</c> if a custom buffer size is set, i.e., <see cref="BufferSize"/> is not equal to
    /// <see cref="DefaultBufferSize"/>.
    /// </summary>
    internal bool HasCustomBufferSize => (_bufferSize ?? DefaultBufferSize) != DefaultBufferSize;

    /// <summary>
    /// Returns a copy of the options with file I/O specific settings applied:
    /// <list type="bullet">
    /// <item>Sets <see cref="LeaveOpen"/> to <c>false</c> to ensure file handles are always closed</item>
    /// <item>Sets <see cref="BufferSize"/> to <see cref="DefaultFileBufferSize"/> if a custom buffer size is not configured</item>
    /// </list>
    /// </summary>
    public CsvIOOptions ForFileIO()
    {
        return this with
        {
            // never leave library-created file streams open!
            LeaveOpen = false,

            // use large buffer size for file I/O if no user overridden buffer size
            BufferSize = HasCustomBufferSize ? BufferSize : DefaultFileBufferSize,
        };
    }
}
