using static FlameCsv.Utilities.SealableUtil;

namespace FlameCsv;

public partial class CsvReaderOptions<T> : ICsvDialectOptions<T>
{
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
}
