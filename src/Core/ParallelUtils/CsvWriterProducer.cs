using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.IO;
using FlameCsv.Writing;

namespace FlameCsv.ParallelUtils;

/// <summary>
/// Producer of CSV field writers.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TValue"></typeparam>
internal readonly struct CsvWriterProducer<T, TValue> : IProducer<TValue, CsvFieldWriter<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;
    private readonly IDematerializer<T, TValue> _dematerializer;
    private readonly Action<ReadOnlySpan<T>>? _sink;
    private readonly Func<ReadOnlyMemory<T>, CancellationToken, ValueTask>? _asyncSink;

    public CsvWriterProducer(
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Action<ReadOnlySpan<T>> sink
    )
    {
        _options = options;
        _dematerializer = dematerializer;
        _sink = sink;
    }

    public CsvWriterProducer(
        CsvOptions<T> options,
        IDematerializer<T, TValue> dematerializer,
        Func<ReadOnlyMemory<T>, CancellationToken, ValueTask> asyncSink
    )
    {
        _options = options;
        _dematerializer = dematerializer;
        _asyncSink = asyncSink;
    }

    public void BeforeLoop()
    {
        if (_options.HasHeader)
        {
            using var writer = CreateState();
            _dematerializer.WriteHeader(in writer);
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
            _dematerializer.WriteHeader(in writer);
            writer.WriteNewline();
            await writer.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            await writer.Writer.CompleteAsync(null, cancellationToken).ConfigureAwait(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Produce(TValue input, ref CsvFieldWriter<T> state)
    {
        _dematerializer.Write(ref state, input);
        state.WriteNewline();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvFieldWriter<T> CreateState()
    {
        return new CsvFieldWriter<T>(new MemoryPoolBufferWriter<T>(_sink, _asyncSink, _options.Allocator), _options);
    }

    public void OnException(Exception exception)
    {
        throw new CsvWriteException("An error occurred while formatting CSV.", exception);
    }
}
