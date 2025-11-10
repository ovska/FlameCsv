using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Converters;

/// <summary>
/// Pooling string converter for UTF-8.
/// This converter uses a <see cref="StringPool"/> to manage string instances,
/// which will lower raw parsing throughput but reduce memory usage when parsing many strings with the same values.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CsvPoolingStringUtf8Converter : CsvConverter<byte, string>
{
    /// <summary>
    /// Singleton instance of the <see cref="CsvPoolingStringUtf8Converter"/> using the shared string pool.
    /// </summary>
    public static CsvPoolingStringUtf8Converter Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvPoolingStringUtf8Converter"/> class
    /// with the shared string pool.
    /// </summary>
    public CsvPoolingStringUtf8Converter()
        : this(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvPoolingStringUtf8Converter"/> class
    /// with the specified string pool.
    /// </summary>
    /// <param name="pool">
    /// Pool to use, or <c>null</c> to use the shared pool.
    /// </param>
    [CLSCompliant(false)]
    public CsvPoolingStringUtf8Converter(StringPool? pool)
    {
        Pool = pool ?? StringPool.Shared;
    }

    /// <summary>
    /// Returns the string pool used by this converter.
    /// </summary>
    [CLSCompliant(false)]
    public StringPool Pool { get; }

    /// <inheritdoc />
    public override bool TryParse(ReadOnlySpan<byte> source, [MaybeNullWhen(false)] out string value)
    {
        if (source.IsEmpty)
        {
            value = "";
            return true;
        }

        int length = Encoding.UTF8.GetMaxCharCount(source.Length);

        if (Token<char>.CanStackalloc(length))
        {
            Span<char> buffer = stackalloc char[length];
            int written = Encoding.UTF8.GetChars(source, buffer);
            value = Pool.GetOrAdd(buffer[..written]);
        }
        else
        {
            value = Pool.GetOrAdd(source, Encoding.UTF8);
        }

        return true;
    }

    /// <inheritdoc />
    public override bool TryFormat(Span<byte> destination, string value, out int charsWritten)
    {
        return Encoding.UTF8.TryGetBytes(value, destination, out charsWritten);
    }
}
