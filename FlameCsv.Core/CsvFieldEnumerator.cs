using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;

namespace FlameCsv;

public struct CsvFieldEnumerator<T> : IEnumerator<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    private readonly CsvEnumerationState<T> _state;
    private readonly int _version;
    private readonly bool _disposeState;
    private int _index;

    public ReadOnlyMemory<T> Current { get; private set; }
    object IEnumerator.Current => Current;

    public CsvFieldEnumerator(
        ReadOnlyMemory<T> data,
        CsvReaderOptions<T> options)
        : this(
              data,
              options != null ? new CsvDialect<T>(options) : throw new ArgumentNullException(nameof(options)),
              options.ArrayPool)
    {
    }

    /// <summary>
    /// Initializes a new field enumerator for the specified CSV record.
    /// </summary>
    /// <remarks>
    /// Creating an enumerator this way always causes an allocation for the data if it contains quotes.
    /// To read multiple CSV records with efficient memory usage, use the GetEnumerable-methods on <see cref="CsvReader"/>.
    /// </remarks>
    /// <param name="data">Complete CSV record without trailing newline</param>
    /// <param name="dialect">Dialect used to parse the fields</param>
    /// <param name="arrayPool">
    /// Pool used to unescape possible quoted fields. Set to null to allocate instead. If set, remember to dispose the enumerator
    /// either via <c>foreach</c> or explicitly.
    /// </param>
    public CsvFieldEnumerator(ReadOnlyMemory<T> data, CsvDialect<T> dialect, ArrayPool<T>? arrayPool = null)
    {
        dialect.EnsureValid();

        int quoteCount = data.Span.Count(dialect.Quote);

        if (quoteCount % 2 != 0)
        {
            ThrowForUnevenQuotes(data.Span, quoteCount, in dialect);
        }

        CsvEnumerationState<T> state = new(dialect, arrayPool);
        state.Initialize(data, quoteCount);

        _state = state;
        _disposeState = true;
        _version = state.Version;
        _index = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldEnumerator(CsvEnumerationState<T> state)
    {
        _state = state;
        _version = state.Version;
        _index = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IDisposable.Dispose()
    {
        if (_disposeState)
            _state.Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_version != _state.Version)
        {
            ThrowHelper.ThrowInvalidOperationException("The enumerator cannot be used after the next record has been read.");
        }

        if (_state.TryGetAtIndex(_index, out ReadOnlyMemory<T> column))
        {
            Current = column;
            _index++;
            return true;
        }

        return false;
    }

    public void Reset() => _index = 0;

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowForUnevenQuotes(ReadOnlySpan<T> data, int quoteCount, in CsvDialect<T> dialect)
    {
        throw new ArgumentException(
            $"The data had an uneven amount of quotes ({quoteCount}): {data.AsPrintableString(false, in dialect)}");
    }
}

