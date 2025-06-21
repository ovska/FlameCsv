using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Converters;

/// <summary>
/// Pooling string converter for UTF-16.
/// This converter uses a <see cref="StringPool"/> to manage string instances,
/// which will lower raw parsing throughput but reduce memory usage when parsing many strings with the same values.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CsvPoolingStringTextConverter : CsvConverter<char, string>
{
    /// <summary>
    /// Singleton instance of the <see cref="CsvPoolingStringTextConverter"/> using the shared string pool.
    /// </summary>
    public static CsvPoolingStringTextConverter Instance { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvPoolingStringTextConverter"/> class
    /// with the shared string pool.
    /// </summary>
    public CsvPoolingStringTextConverter()
        : this(null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvPoolingStringTextConverter"/> class
    /// with the specified string pool.
    /// </summary>
    /// <param name="pool">
    /// Pool to use, or <c>null</c> to use the shared pool.
    /// </param>
    [CLSCompliant(false)]
    public CsvPoolingStringTextConverter(StringPool? pool)
    {
        Pool = pool ?? StringPool.Shared;
    }

    /// <summary>
    /// Returns the string pool used by this converter.
    /// </summary>
    [CLSCompliant(false)]
    public StringPool Pool { get; }

    /// <inheritdoc />
    public override bool TryFormat(Span<char> destination, string value, out int charsWritten)
    {
        // use String.TryCopyTo to avoid conversion to span
        if (value.TryCopyTo(destination))
        {
            charsWritten = value.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <inheritdoc />
    public override bool TryParse(ReadOnlySpan<char> source, [MaybeNullWhen(false)] out string value)
    {
        value = Pool.GetOrAdd(source);
        return true;
    }
}
