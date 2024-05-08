using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public struct CsvFieldEnumerator<T> : IDisposable, IEnumerator<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    public ReadOnlyMemory<T> Current { get; private set; }

    readonly object IEnumerator.Current => Current;

    private readonly CsvReadingContext<T> _context;
    private T[]? _toReturn;

    private ReadOnlyMemory<T> _remaining;
    private RecordMeta _remainingMeta;
    private bool _isAtStart;

    internal CsvFieldEnumerator(ReadOnlyMemory<T> value, in CsvReadingContext<T> context)
    {
        Throw.IfDefaultStruct<CsvFieldEnumerator<T>>(context.ArrayPool);

        _context = context;
        _remaining = value;
        _isAtStart = true;
        _remainingMeta = context.GetRecordMeta(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_remaining.IsEmpty)
        {
            return false;
        }

        var reader = new CsvFieldReader<T>(
            _remaining,
            in _context,
            [],
            ref _toReturn,
            _remainingMeta.quoteCount,
            _remainingMeta.escapeCount);

        reader.isAtStart = _isAtStart;

        if (!reader.TryReadNext(out ReadOnlyMemory<T> field))
        {
            _remaining = default;
            _remainingMeta = default;
            return false;
        }

        _remaining = _remaining.Slice(reader.Consumed);
        _remainingMeta.quoteCount = reader.quotesRemaining;
        _remainingMeta.escapeCount = reader.escapesRemaining;
        _isAtStart = reader.isAtStart;
        Current = field;
        return true;
    }

    public readonly void Reset() => throw new NotSupportedException();

    public void Dispose()
    {
        _context.ArrayPool.EnsureReturned(ref _toReturn);
    }
}
