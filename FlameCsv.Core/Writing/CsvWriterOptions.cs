using System.Buffers;
using CommunityToolkit.HighPerformance;
using System.Text;
using FlameCsv.Formatters;
using FlameCsv.Formatters.Text;
using FlameCsv.Utilities;
using static FlameCsv.Utilities.SealableUtil;

namespace FlameCsv.Writing;

public class CsvWriterOptions<T> : ICsvDialectOptions<T>, ISealable
    where T : unmanaged, IEquatable<T>
{
    public IDictionary<Type, ReadOnlyMemory<T>> NullOverrides => _nullOverrides ??= new();
    private Dictionary<Type, ReadOnlyMemory<T>>? _nullOverrides;

    public ReadOnlyMemory<T> Null { get; set; }

    public ReadOnlyMemory<T> GetNullToken(Type type)
    {
        if (_nullOverrides is not null && _nullOverrides.TryGetValue(type, out var value))
            return value;

        return Null;
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

    internal T _delimiter;
    internal T _quote;
    internal ReadOnlyMemory<T> _newline;
    internal T? _escape;

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

    public virtual void WriteChars<TWriter>(TWriter writer, ReadOnlySpan<char> value) where TWriter : IBufferWriter<T>
    {
        if (value.IsEmpty)
            return;

        if (typeof(T) == typeof(char))
        {
            Span<T> destination = writer.GetSpan(value.Length);
            value.CopyTo(destination.Cast<T, char>());
            writer.Advance(value.Length);
        }
        else if (typeof(T) == typeof(byte))
        {
            Span<T> destination = writer.GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length));
            int written = Encoding.UTF8.GetBytes(value, destination.Cast<T, byte>());
            writer.Advance(written);
        }
        else
        {
            Token<T>.ThrowNotSupportedException();
        }
    }
}
