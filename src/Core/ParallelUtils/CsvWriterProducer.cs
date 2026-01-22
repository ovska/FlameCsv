using System.Runtime.CompilerServices;
using FlameCsv.IO;
using FlameCsv.Writing;

namespace FlameCsv.ParallelUtils;

/// <summary>
/// Producer of CSV field writers.
/// </summary>
internal readonly struct CsvWriterProducer<T, TValue, TChunk> : IProducer<TValue, CsvFieldWriter<T>, TChunk>
    where T : unmanaged, IBinaryInteger<T>
    where TChunk : IHasOrder
{
    private readonly CsvOptions<T> _options;
    private readonly IDematerializer<T, TValue> _dematerializer;
    private readonly Action<ReadOnlySpan<T>, bool>? _sink;
    private readonly Func<ReadOnlyMemory<T>, bool, CancellationToken, ValueTask>? _asyncSink;
    private readonly CsvIOOptions _ioOptions;

    public CsvWriterProducer(
        CsvOptions<T> options,
        CsvIOOptions ioOptions,
        IDematerializer<T, TValue> dematerializer,
        Action<ReadOnlySpan<T>, bool> sink
    )
    {
        _options = options;
        _ioOptions = ioOptions;
        _dematerializer = dematerializer;
        _sink = sink;
    }

    public CsvWriterProducer(
        CsvOptions<T> options,
        CsvIOOptions ioOptions,
        IDematerializer<T, TValue> dematerializer,
        Func<ReadOnlyMemory<T>, bool, CancellationToken, ValueTask> asyncSink
    )
    {
        _options = options;
        _ioOptions = ioOptions;
        _dematerializer = dematerializer;
        _asyncSink = asyncSink;
    }

    public void BeforeLoop(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_options.HasHeader)
        {
            using var writer = CreateState();
            _dematerializer.WriteHeader(writer);
            writer.WriteNewline();
            writer.Writer.Drain();
            writer.Writer.Complete(null);
        }
    }

    public async ValueTask BeforeLoopAsync(CancellationToken cancellationToken)
    {
        if (_options.HasHeader)
        {
            using var writer = CreateState();
            _dematerializer.WriteHeader(writer);
            writer.WriteNewline();
            await writer.Writer.DrainAsync(cancellationToken).ConfigureAwait(false);
            await writer.Writer.CompleteAsync(null, cancellationToken).ConfigureAwait(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Produce(TChunk _, TValue input, CsvFieldWriter<T> state)
    {
        _dematerializer.Write(state, input);
        state.WriteNewline();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvFieldWriter<T> CreateState()
    {
        return new CsvFieldWriter<T>(new MemoryPoolBufferWriter<T>(_sink, _asyncSink, in _ioOptions), _options);
    }
}

internal sealed class CsvWriterConsumer<T> : IConsumer<CsvFieldWriter<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    public static CsvWriterConsumer<T> Instance { get; } = new();

    public void Consume(CsvFieldWriter<T> state, Exception? ex)
    {
        try
        {
            if (ex is null)
            {
                state.Writer.Drain();
            }
        }
        catch (Exception e)
        {
            ex = e;
            throw;
        }
        finally
        {
            state.Dispose();
            state.Writer.Complete(ex);
        }
    }

    public async ValueTask ConsumeAsync(CsvFieldWriter<T> state, Exception? ex, CancellationToken cancellationToken)
    {
        try
        {
            if (ex is null)
            {
                await state.Writer.DrainAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            ex = e;
            throw;
        }
        finally
        {
            state.Dispose();
            await state.Writer.CompleteAsync(ex, cancellationToken).ConfigureAwait(false);
        }
    }
}
