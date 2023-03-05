using System.Runtime.CompilerServices;

namespace FlameCsv;

public partial class CsvReaderOptions<T>
{
    /// <summary>
    /// Returns a new read-only options instance using the default text or UTF8 configuration.
    /// </summary>
    /// <returns>
    /// Instance of <see cref="CsvTextReaderOptions"/> for <see langword="char"/>.<br/>
    /// Instance of <see cref="CsvUtf8ReaderOptions"/> for <see langword="byte"/>.<br/>
    /// Throws for other types of <typeparamref name="T"/>.
    /// </returns>
    /// <exception cref="NotSupportedException"><typeparamref name="T"/> is not char or byte</exception>
    /// <seealso cref="CsvTextReaderOptions.Default"/>
    /// <seealso cref="CsvUtf8ReaderOptions.Default"/>
    public static CsvReaderOptions<T> Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // type checks are JITed out
            if (typeof(T) == typeof(char))
                return (CsvReaderOptions<T>)(object)CsvReaderOptionsDefaults.Text;

            if (typeof(T) == typeof(byte))
                return (CsvReaderOptions<T>)(object)CsvReaderOptionsDefaults.Utf8;

            throw new NotSupportedException($"Default configuration for {typeof(T)} is not supported.");
        }
    }
}

// separate class to avoid statics in the generic CsvReaderOptions<T>
internal static class CsvReaderOptionsDefaults
{
    public static CsvTextReaderOptions Text => _textLazy.Value;
    public static CsvUtf8ReaderOptions Utf8 => _utf8Lazy.Value;

    public static readonly Lazy<CsvTextReaderOptions> _textLazy = new Lazy<CsvTextReaderOptions>(
        static () =>
        {
            var options = new CsvTextReaderOptions();
            options.MakeReadOnly();
            return options;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    public static readonly Lazy<CsvUtf8ReaderOptions> _utf8Lazy = new Lazy<CsvUtf8ReaderOptions>(
        static () =>
        {
            var options = new CsvUtf8ReaderOptions();
            options.MakeReadOnly();
            return options;
        }, LazyThreadSafetyMode.ExecutionAndPublication);
}
