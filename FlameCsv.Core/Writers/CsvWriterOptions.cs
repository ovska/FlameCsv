using System.Buffers;
using System.Reflection;
using FlameCsv.Configuration;
using FlameCsv.Formatters;
using FlameCsv.Utilities;
using static FlameCsv.Utilities.SealableUtil;

namespace FlameCsv.Writers;

public class CsvWriterOptions<T> : ICsvDialectOptions<T>, ISealable,
    ICsvNullTokenConfiguration<T>,
    ICsvFormatProviderConfiguration<T>,
    ICsvFormatConfiguration<T>
    where T : unmanaged, IEquatable<T>
{
    public bool WriteHeader { get; set; }

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

    public ArrayPool<T>? ArrayPool { get; set; }

    internal CsvDialect<T> Dialect { get; }

    public ICsvFormatter<T> GetFormatter(Type type) => throw new NotImplementedException();

    public ICsvFormatter<T, TValue> GetFormatter<TValue>() => throw new NotImplementedException();

    public bool IsReadOnly { get; private set; }

    public bool MakeReadOnly()
    {
        throw new NotImplementedException();
    }

    protected internal T _delimiter;
    protected internal T _quote;
    protected internal ReadOnlyMemory<T> _newline;
    protected internal ReadOnlyMemory<T> _whitespace;
    protected internal T? _escape;

    public T Delimiter
    {
        get => _delimiter;
        set => this.SetValue(ref _delimiter, value);
    }

    public T Quote
    {
        get => _quote;
        set => this.SetValue(ref _quote, value);
    }

    public ReadOnlyMemory<T> Newline
    {
        get => _newline;
        set => this.SetValue(ref _newline, value);
    }

    public ReadOnlyMemory<T> Whitespace
    {
        get => _whitespace;
        set => this.SetValue(ref _whitespace, value);
    }

    public T? Escape
    {
        get => _escape;
        set => this.SetValue(ref _escape, value);
    }
}
