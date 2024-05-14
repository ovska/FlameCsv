using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public struct CsvFieldEnumerator<T> : IDisposable, IEnumerator<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    public ReadOnlyMemory<T> Current { get; private set; }

    readonly object IEnumerator.Current => Current;

    private readonly CsvParser<T> _parser;
    private T[]? _toReturn;

    private ReadOnlyMemory<T> _remaining;
    private CsvRecordMeta _remainingMeta;
    private bool _isAtStart;

    public CsvFieldEnumerator(ReadOnlyMemory<T> value, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _parser = CsvParser<T>.Create(options);
        _remaining = value;
        _isAtStart = true;
        _remainingMeta = _parser.GetRecordMeta(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_remaining.IsEmpty)
        {
            return false;
        }

        var reader = new CsvFieldReader<T>(
            _parser._options,
            _remaining,
            [],
            ref _toReturn,
            _remainingMeta.quoteCount,
            _remainingMeta.escapeCount)
        { isAtStart = _isAtStart };

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
        _parser._arrayPool.EnsureReturned(ref _toReturn);
        _parser.Dispose();
    }
}
