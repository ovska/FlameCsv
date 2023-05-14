namespace FlameCsv;

internal static class CsvReaderOptionsDefaults
{
    public static CsvTextOptions Text => _textLazy.Value;
    public static CsvUtf8Options Utf8 => _utf8Lazy.Value;

    public static readonly Lazy<CsvTextOptions> _textLazy = new(
        static () =>
        {
            var options = new CsvTextOptions();
            options.MakeReadOnly();
            return options;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

    public static readonly Lazy<CsvUtf8Options> _utf8Lazy = new(
        static () =>
        {
            var options = new CsvUtf8Options();
            options.MakeReadOnly();
            return options;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
}
