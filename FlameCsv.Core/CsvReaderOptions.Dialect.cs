namespace FlameCsv;

public partial class CsvReaderOptions<T> : ICsvDialectOptions<T>
{
    protected internal T _delimiter;
    protected internal T _quote;
    protected internal ReadOnlyMemory<T> _newline;
    protected internal ReadOnlyMemory<T> _whitespace;
    protected internal T? _escape;

    public T Delimiter
    {
        get => _delimiter;
        set => SetValue(ref _delimiter, value);
    }

    public T Quote
    {
        get => _quote;
        set => SetValue(ref _quote, value);
    }

    public ReadOnlyMemory<T> Newline
    {
        get => _newline;
        set => SetValue(ref _newline, value);
    }

    public ReadOnlyMemory<T> Whitespace
    {
        get => _whitespace;
        set => SetValue(ref _whitespace, value);
    }

    public T? Escape
    {
        get => _escape;
        set => SetValue(ref _escape, value);
    }
}
