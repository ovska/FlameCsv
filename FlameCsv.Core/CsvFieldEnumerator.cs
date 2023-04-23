using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public struct CsvFieldEnumerator<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public ReadOnlyMemory<T> Current { get; private set; }

    private readonly ArrayPool<T>? _arrayPool;
    private T[]? _toReturn;

    private CsvEnumerationStateRef<T> _state;
    private readonly CsvEnumerationState<T>? _source;
    private readonly int _version;

    internal CsvFieldEnumerator(ReadOnlyMemory<T> value, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        var dialect = new CsvDialect<T>(options);
        _arrayPool = options.ArrayPool.AllocatingIfNull();
        _state = new CsvEnumerationStateRef<T>(
            dialect: in dialect,
            record: value,
            remaining: value,
            isAtStart: true,
            meta: dialect.GetRecordMeta(value, options.AllowContentInExceptions),
            array: ref _toReturn,
            arrayPool: _arrayPool,
            exposeContent: options.AllowContentInExceptions);
    }

    internal CsvFieldEnumerator(ReadOnlyMemory<T> value, CsvEnumerationState<T> state, RecordMeta meta)
    {
        _source = state;
        _version = state.Version;
        _state = state.GetInitialStateFor(value, meta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        _source?.EnsureVersion(_version);

        if (_state.TryGetField(out ReadOnlyMemory<T> field))
        {
            Current = field;
            return true;
        }

        Current = default;
        return false;
    }

    public void Reset() => throw new NotSupportedException();

    public void Dispose()
    {
        _arrayPool?.EnsureReturned(ref _toReturn);
    }
}
