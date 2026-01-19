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
    private readonly Action<ReadOnlySpan<T>>? _sink;
    private readonly Func<ReadOnlyMemory<T>, CancellationToken, ValueTask>? _asyncSink;
    private readonly CsvIOOptions _ioOptions;

    public CsvWriterProducer(
        CsvOptions<T> options,
        CsvIOOptions ioOptions,
        IDematerializer<T, TValue> dematerializer,
        Action<ReadOnlySpan<T>> sink
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
        Func<ReadOnlyMemory<T>, CancellationToken, ValueTask> asyncSink
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
            writer.Writer.Flush();
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
            await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await writer.Writer.CompleteAsync(null, cancellationToken).ConfigureAwait(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Produce(TChunk _, TValue input, ref CsvFieldWriter<T> state)
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

    public void Consume(in CsvFieldWriter<T> state, Exception? ex)
    {
        using (state)
        {
            try
            {
                if (ex is null)
                {
                    state.Writer.Flush();
                }
            }
            catch (Exception e)
            {
                ex ??= e;
                throw;
            }
            finally
            {
                state.Writer.Complete(ex);
            }
        }
    }

    public async ValueTask ConsumeAsync(CsvFieldWriter<T> state, Exception? ex, CancellationToken cancellationToken)
    {
        using (state)
        {
            try
            {
                if (ex is null)
                {
                    await state.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                ex ??= e;
                throw;
            }
            finally
            {
                await state.Writer.CompleteAsync(ex, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
