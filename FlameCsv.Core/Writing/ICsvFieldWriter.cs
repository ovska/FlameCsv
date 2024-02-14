using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Writing;

public interface ICsvFieldWriter<T> : IAsyncDisposable where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Whether the writer's internal buffer is nearly full.
    /// </summary>
    bool NeedsFlush { get; }

    /// <summary>
    /// Flushes the writer's internal buffer.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a single CSV field to the writer.
    /// </summary>
    /// <typeparam name="TValue">Value type</typeparam>
    /// <param name="converter">Converter to format the value with</param>
    /// <param name="value">Value to write</param>
    void WriteField<TValue>(CsvConverter<T, TValue> converter, [AllowNull] TValue value);

    /// <summary>
    /// Writes the delimiter token.
    /// </summary>
    void WriteDelimiter();

    /// <summary>
    /// Writes the newline token.
    /// </summary>
    void WriteNewline();

    /// <summary>
    /// Writes <paramref name="text"/>.
    /// </summary>
    void WriteText(ReadOnlySpan<char> text);

    /// <summary>
    /// Writes the <paramref name="span"/> contents unvalidated to the writer.
    /// </summary>
    void WriteRaw(ReadOnlySpan<T> span);
}
