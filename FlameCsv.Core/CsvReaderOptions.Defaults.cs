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

internal static class CsvReaderOptionsDefaults
{
    public static CsvTextReaderOptions? _textDefault;
    public static CsvUtf8ReaderOptions? _utf8Default;

    public static CsvTextReaderOptions Text
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _textDefault ?? GetOrInitializeTextDefaults();
    }

    public static CsvUtf8ReaderOptions Utf8
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _utf8Default ?? GetOrInitializeUtf8Defaults();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static CsvTextReaderOptions GetOrInitializeTextDefaults()
    {
        var options = new CsvTextReaderOptions();
        options.MakeReadOnly();
        return Interlocked.CompareExchange(ref _textDefault, options, null) ?? options;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static CsvUtf8ReaderOptions GetOrInitializeUtf8Defaults()
    {

        var options = new CsvUtf8ReaderOptions();
        options.MakeReadOnly();
        return Interlocked.CompareExchange(ref _utf8Default, options, null) ?? options;
    }
}
