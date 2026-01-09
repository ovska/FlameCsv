using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.IO.Internal;
using FlameCsv.Reading;

namespace FlameCsv.ParallelUtils;

internal sealed class ValueProducer<T, TValue> : IProducer<CsvRecordRef<T>, SlimList<TValue>, Chunk<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static ValueProducer<T, TValue> Create(CsvOptions<T>? options, CsvParallelOptions parallelOptions)
    {
        return new(
            chunkSize: parallelOptions.EffectiveChunkSize,
            options: options ?? CsvOptions<T>.Default,
            materializerFactory: static (h, o) => o.TypeBinder.GetMaterializer<TValue>(h),
            headerlessFactory: static o => o.TypeBinder.GetMaterializer<TValue>()
        );
    }

    public static ValueProducer<T, TValue> Create(
        CsvTypeMap<T, TValue> typeMap,
        CsvOptions<T>? options,
        CsvParallelOptions parallelOptions
    ) =>
        new(
            chunkSize: parallelOptions.EffectiveChunkSize,
            options: options ?? CsvOptions<T>.Default,
            materializerFactory: typeMap.GetMaterializer,
            headerlessFactory: typeMap.GetMaterializer
        );

    public CsvOptions<T> Options { get; }

    private readonly int _chunkSize;
    private readonly Func<ImmutableArray<string>, CsvOptions<T>, IMaterializer<T, TValue>> _materializerFactory;
    private readonly ManualResetEventSlim? _headerRead;
    private IMaterializer<T, TValue>? _materializer;
    private CancellationToken _cancellationToken;
    private ImmutableArray<string> _headers;

    public ValueProducer(
        int chunkSize,
        CsvOptions<T> options,
        Func<ImmutableArray<string>, CsvOptions<T>, IMaterializer<T, TValue>> materializerFactory,
        Func<CsvOptions<T>, IMaterializer<T, TValue>> headerlessFactory
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        _chunkSize = chunkSize;
        Options = options;
        _materializerFactory = materializerFactory;

        if (!options.HasHeader)
        {
            _materializer = headerlessFactory(options);
            _headerRead = null!;
        }
        else
        {
            _headerRead = new ManualResetEventSlim();
        }
    }

    public void BeforeLoop(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public ValueTask BeforeLoopAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return ValueTask.CompletedTask;
    }

    public SlimList<TValue> CreateState() => new(_chunkSize);

    public void Dispose()
    {
        _headerRead?.Dispose();
    }

    public void Produce(Chunk<T> chunk, CsvRecordRef<T> input, ref SlimList<TValue> state)
    {
        if (TryProduceDirect(chunk, input, out TValue? result))
        {
            state.Add(result);
        }
    }

    public bool TryProduceDirect(Chunk<T> chunk, CsvRecordRef<T> input, [MaybeNullWhen(false)] out TValue value)
    {
        if (Options.RecordCallback is { } callback)
        {
            bool skip = false;
            bool headerRead = !_headers.IsDefaultOrEmpty;
            CsvRecordCallbackArgs<T> args = new(input, _headers, ref skip, ref headerRead);

            callback(in args);

            if (skip)
            {
                value = default;
                return false;
            }
        }

        if (_materializer is null)
        {
            Check.True(Options.HasHeader, "Options must be configured to read with a header");
            Check.NotNull(_headerRead, "Header read event must be initialized");

            if (chunk.Order == 0)
            {
                _headers = CsvHeader.Parse(input);
                _materializer = _materializerFactory(_headers, Options);
                _headerRead.Set();
                value = default;
                return false;
            }
            else
            {
                // some other thread is still creating the materializer
                Check.True(_cancellationToken.CanBeCanceled, "CT must be cancelable to avoid deadlocks");
                _headerRead.Wait(_cancellationToken);
                Check.NotNull(_materializer, "Materializer must be initialized after header is read");
            }
        }

        try
        {
            value = _materializer.Parse(input);
        }
        catch (CsvFormatException cfe) // unrecoverable
        {
            cfe.Enrich(input);
            throw;
        }
        catch (Exception ex)
        {
            Check.WrapParseError(ref ex);

            (ex as CsvReadExceptionBase)?.Enrich(input);
            (ex as CsvParseException)?.WithHeader(_headers);
            (ex as CsvReadException)?.ChunkOrder = chunk.Order;

            if (
                Options.ExceptionHandler?.Invoke(
                    new CsvExceptionHandlerArgs<T>(in input, _headers, ex, (ex as CsvReadException)?.ExpectedFieldCount)
                ) == true
            )
            {
                // exception handled; try again
                value = default;
                return false;
            }

            throw;
        }
        return true;
    }
}
