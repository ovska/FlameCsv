using static FlameCsv.Utilities.SealableUtil;

namespace FlameCsv;

public partial class CsvReaderOptions<T> : ICsvDialectOptions<T>
{
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
