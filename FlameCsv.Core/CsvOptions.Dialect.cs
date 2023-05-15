using static FlameCsv.Utilities.SealableUtil;

namespace FlameCsv;

public partial class CsvOptions<T> : ICsvDialectOptions<T>
{
    internal T _delimiter;
    internal T _quote;
    internal ReadOnlyMemory<T> _newline;
    internal T? _escape;

    T ICsvDialectOptions<T>.Delimiter
    {
        get => _delimiter;
        set => this.SetValue(ref _delimiter, value);
    }

    T ICsvDialectOptions<T>.Quote
    {
        get => _quote;
        set => this.SetValue(ref _quote, value);
    }

    ReadOnlyMemory<T> ICsvDialectOptions<T>.Newline
    {
        get => _newline;
        set => this.SetValue(ref _newline, value);
    }

    T? ICsvDialectOptions<T>.Escape
    {
        get => _escape;
        set => this.SetValue(ref _escape, value);
    }
}
