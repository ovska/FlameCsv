namespace FlameCsv;

internal static class CsvReaderOptionsDefaults
{
    public static CsvTextReaderOptions Text => _textLazy.Value;
    public static CsvUtf8ReaderOptions Utf8 => _utf8Lazy.Value;

    public static readonly Lazy<CsvTextReaderOptions> _textLazy = new(
        static () =>
        {
            var options = new CsvTextReaderOptions();
            options.MakeReadOnly();
            return options;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

    public static readonly Lazy<CsvUtf8ReaderOptions> _utf8Lazy = new(
        static () =>
        {
            var options = new CsvUtf8ReaderOptions();
            options.MakeReadOnly();
            return options;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
}
