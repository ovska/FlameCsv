using FlameCsv.Formatters;

namespace FlameCsv.Writers;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public class CsvWriterOptions<T> where T : unmanaged, IEquatable<T>
{
    public IList<ICsvFormatter<T>> Formatters { get; } = new List<ICsvFormatter<T>>();

    /// <summary>
    /// Whether a final newline is written after the last record. Default is <see langword="false"/>.
    /// </summary>
    public bool WriteFinalNewline { get; set; }

    /// <summary>
    /// Whether to skip escaping the output altogether. Use this with caution, as this can cause
    /// invalid CSV to be written if the formatters output data with delimiters, string delimiters, or 
    /// newline characters. Default is <see langword="false"/>.
    /// </summary>
    public bool DangerousNoEscaping { get; set; }

    public ICsvFormatter<T> GetFormatter(Type type) => throw new NotImplementedException();

    public ICsvFormatter<T, TValue> GetFormatter<TValue>() => throw new NotImplementedException();
}
