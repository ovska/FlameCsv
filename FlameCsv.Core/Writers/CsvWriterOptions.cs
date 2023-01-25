using FlameCsv.Formatters;

namespace FlameCsv.Writers;

public sealed class CsvWriterOptions<T> where T : unmanaged, IEquatable<T>
{
    public IList<ICsvFormatter<T>> Formatters { get; } = new List<ICsvFormatter<T>>();

    /// <summary>
    /// Whether a final newline is written after the last record. Default is <see langword="false"/>.
    /// </summary>
    public bool WriteFinalNewline { get; set; }
}
