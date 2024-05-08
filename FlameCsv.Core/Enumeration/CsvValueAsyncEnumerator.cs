using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

public sealed class CsvValueAsyncEnumerator<T, TValue> : CsvValueEnumeratorBase<T, TValue>, IAsyncEnumerator<TValue>
    where T : unmanaged, IEquatable<T>
{
    private bool _readerCompleted;

    private readonly ICsvPipeReader<T> _reader;
    private readonly CancellationToken _cancellationToken;

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    internal CsvValueAsyncEnumerator(
        in CsvReadingContext<T> context,
        ICsvPipeReader<T> reader,
        CancellationToken cancellationToken)
        : base(in context)
    {
        _reader = reader;
        _cancellationToken = cancellationToken;
    }

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    internal CsvValueAsyncEnumerator(
        in CsvReadingContext<T> context,
        IMaterializer<T, TValue>? materializer,
        ICsvPipeReader<T> reader,
        CancellationToken cancellationToken) : base(in context, materializer)
    {
        _reader = reader;
        _cancellationToken = cancellationToken;
    }

    internal CsvValueAsyncEnumerator(
        in CsvReadingContext<T> context,
        CsvTypeMap<T, TValue> typeMap,
        ICsvPipeReader<T> reader,
        CancellationToken cancellationToken) : base(in context, typeMap)
    {
        _reader = reader;
        _cancellationToken = cancellationToken;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<bool>(_cancellationToken);
        }

        if (TryRead(isFinalBlock: false))
        {
            return new ValueTask<bool>(true);
        }

        return MoveNextAsyncCore();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<bool> MoveNextAsyncCore()
    {
        while (!_readerCompleted)
        {
            _reader.AdvanceTo(consumed: _data.Reader.Position, examined: _data.Reader.Sequence.End);

            var (sequence, readerCompleted) = await _reader.ReadAsync(_cancellationToken).ConfigureAwait(false);

            _data.Reset(in sequence);
            _readerCompleted = readerCompleted;

            if (TryRead(isFinalBlock: false))
            {
                return true;
            }
        }

        return TryRead(isFinalBlock: true);
    }

    public async ValueTask DisposeAsync()
    {
        await _reader.DisposeAsync().ConfigureAwait(false);
        base.Dispose(true);
    }

    protected override void Dispose(bool disposing)
    {
        _reader.Dispose();
        base.Dispose(disposing);
    }
}
