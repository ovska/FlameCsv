using FlameCsv.Extensions;

namespace FlameCsv;

public readonly struct CsvFieldEnumerable<T> where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlyMemory<T> _record;
    private readonly CsvReaderOptions<T> _options;
    private readonly CsvEnumerationState<T>? _state;

    public CsvFieldEnumerable(
        ReadOnlyMemory<T> value,
        CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _record = value;
        _options = options;
    }

    internal CsvFieldEnumerable(CsvEnumerationState<T> state, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();
        _state = state;
        _options = options;
    }

    public CsvFieldEnumerator<T> GetEnumerator()
    {
        return _state is null
            ? new(_record, _options)
            : new(_record, _state, _state.Dialect.GetRecordMeta(_record, _options.AllowContentInExceptions));
    }
}
