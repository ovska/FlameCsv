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

    private readonly CsvReadingContext<T> _context;
    private readonly CsvEnumerationState<T>? _source;
    private readonly int _version;

    private CsvEnumerationStateRef<T> _state;

    internal CsvFieldEnumerator(ReadOnlyMemory<T> value, CsvReaderOptions<T> options)
        : this(value, new CsvReadingContext<T>(options))
    {
    }

    internal CsvFieldEnumerator(ReadOnlyMemory<T> value, in CsvReadingContext<T> context)
    {
        Throw.IfDefaultStruct<CsvFieldEnumerator<T>>(context.arrayPool);

        _context = context;
        _arrayPool = context.arrayPool;
        _state = new CsvEnumerationStateRef<T>(in context, value, ref _toReturn);
    }

    internal CsvFieldEnumerator(ReadOnlyMemory<T> value, CsvEnumerationState<T> state, RecordMeta? meta = null)
    {
        _source = state;
        _version = state.Version;
        _state = state.GetInitialStateFor(value, meta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        _source?.EnsureVersion(_version);

        if (_context.TryGetField(ref _state, out ReadOnlyMemory<T> field))
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
