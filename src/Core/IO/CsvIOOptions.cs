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
    /// The default buffer size.
    /// </summary>
    public const int DefaultBufferSize = 1024 * 16;

    /// <summary>
    /// The default buffer size when doing file I/O.
    /// </summary>
    public const int DefaultFileBufferSize = 1024 * 64;

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
    /// This value will be clamped to be at minimum 256 or <see cref="MinimumBufferSize"/>, whichever is larger.
    /// </summary>
    /// <remarks>
    /// This is only the initial value; the buffer may be resized during reading if needed.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the value is negative and not -1.
    /// </exception>
    public int BufferSize
    {
        get => _bufferSize ?? DefaultBufferSize;
        init
        {
            if (value == -1)
                return;
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _bufferSize = Math.Max(value, MinimumBufferSize);
        }
    }

    /// <summary>
    /// Gets or sets the minimum available data size when reading.
    /// If unset or set to -1, the default will be used (<see cref="MinimumBufferSize"/> / 2).<br/>
    /// This value will be clamped to be at minimum <see cref="MinimumBufferSize"/> / 2.
    /// </summary>
    /// <remarks>
    /// This threshold determines when more data should be read from the source.
    /// If less than MinimumReadSize bytes are available in the buffer, and more data is available from the source,
    /// more data will be read.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the value is less than 1 and not equal to -1.
    /// </exception>
    public int MinimumReadSize
    {
        get
        {
            int effectiveMinimumReadSize = _minimumReadSize ?? DefaultMinimumReadSize;
            int maxAllowedReadSize = (_bufferSize ?? DefaultBufferSize) / 2;
            return Math.Min(effectiveMinimumReadSize, maxAllowedReadSize);
        }
        init
        {
            if (value == -1)
                return;
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _minimumReadSize = Math.Max(value, MinimumBufferSize / 2);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the inner stream/reader should be left open after use.<br/>
    /// The default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// This parameter is not used if the data source or destination is not user provided, e.g.
    /// <see cref="CsvWriter.WriteToFile{TValue}(string,IEnumerable{TValue},CsvOptions{byte}?,CsvIOOptions)"/>
    /// </remarks>
    public bool LeaveOpen { get; init; }

    /// <summary>
    /// Disable direct buffer reading optimization when reading <see cref="MemoryStream"/> or
    /// <see cref="StringReader"/> with an exposable buffer.<br/>
    /// The default is <c>false</c>.
    /// </summary>
    public bool NoDirectBufferAccess { get; init; }

    internal bool HasCustomBufferSize => _bufferSize.HasValue && _bufferSize != DefaultBufferSize;

    /// <summary>
    /// Returns the options with file I/O specific settings applied.<br/>
    /// This will set <see cref="CsvIOOptions.LeaveOpen"/> to <c>false</c> and
    /// set the <see cref="CsvIOOptions.BufferSize"/> to
    /// <see cref="CsvIOOptions.DefaultFileBufferSize"/> if the user has not set a custom buffer size.
    /// </summary>
    internal CsvIOOptions ForFileIO()
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
