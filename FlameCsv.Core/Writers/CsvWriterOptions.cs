using System.Buffers;
using FlameCsv.Configuration;
using FlameCsv.Formatters;
using FlameCsv.Formatters.Text;
using FlameCsv.Utilities;
using static FlameCsv.Utilities.SealableUtil;

namespace FlameCsv.Writers;

public class CsvWriterOptions<T> : ICsvDialectOptions<T>, ISealable,
    ICsvNullTokenConfiguration<T>
    where T : unmanaged, IEquatable<T>
{
    public IDictionary<Type, ReadOnlyMemory<T>> NullOverrides => _nullOverrides ??= new();
    private Dictionary<Type, ReadOnlyMemory<T>>? _nullOverrides;

    public ReadOnlyMemory<T> Null { get; set; }

    ReadOnlyMemory<T> ICsvNullTokenConfiguration<T>.Default => Null;

    bool ICsvNullTokenConfiguration<T>.TryGetOverride(Type type, out ReadOnlyMemory<T> value)
    {
        if (_nullOverrides is not null && _nullOverrides.TryGetValue(type, out value))
            return true;

        value = default;
        return false;
    }

    public bool WriteHeader { get; set; }

    public IList<ICsvFormatter<T>> Formatters { get; } = new List<ICsvFormatter<T>>();

    /// <summary>
    /// Whether a final newline is written after the last record. Default is <see langword="false"/>.
    /// </summary>
    public bool WriteFinalNewline { get; set; }

    public CsvFieldQuoting FieldQuoting { get; set; }

    public ArrayPool<T>? ArrayPool { get; set; }

    public ICsvFormatter<T> GetFormatter(Type type)
    {
        if (type == typeof(int))
            return (ICsvFormatter<T>)(object)new SpanFormattableTextFormatter<int>();

        if (type == typeof(bool))
            return (ICsvFormatter<T>)(object)new BooleanTextFormatter();

        if (type == typeof(string))
            return (ICsvFormatter<T>)(object)new StringTextFormatter();

        throw new NotImplementedException();
    }

    public ICsvFormatter<T, TValue> GetFormatter<TValue>() => throw new NotImplementedException();

    public bool IsReadOnly { get; private set; }

    public bool MakeReadOnly()
    {
        throw new NotImplementedException();
    }

    public CsvWriterOptions()
    {
        var temp = CsvDialect<T>.Default;
        _delimiter = temp.Delimiter;
        _quote = temp.Quote;
        _newline = temp.Newline;
    }

    protected internal T _delimiter;
    protected internal T _quote;
    protected internal ReadOnlyMemory<T> _newline;
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

    public T? Escape
    {
        get => _escape;
        set => this.SetValue(ref _escape, value);
    }
}
