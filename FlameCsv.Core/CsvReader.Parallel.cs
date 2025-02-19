using System.Buffers;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Parallel;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Provides methods for parallel processing of CSV data.
/// </summary>
[PublicAPI]
public static class CsvParallelReader
{
    /// <summary>
    /// Returns <see langword="true"/> if the options can be used for parallel reading.
    /// </summary>
    /// <param name="options">Options-instance</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <returns>
    /// Whether the options can be used for parallel reading.
    /// </returns>
    public static bool IsSupported<T>(CsvOptions<T> options) where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        return !options.NoReadAhead && options.Dialect.IsAscii;
    }

    /// <summary>
    /// Returns a parallel query for the records in the provided CSV data using a custom selector.
    /// </summary>
    /// <param name="csv">Csv data</param>
    /// <param name="invoker">
    /// Instance used to process each record into <typeparamref name="TValue"/>.
    /// For optimal performance, this type should be a <see langword="readonly"/> <see langword="struct"/>.
    /// </param>
    /// <param name="options">Options-instance</param>
    /// <typeparam name="TValue">Value returned by the invoker</typeparam>
    /// <typeparam name="TInvoke">Invoker type</typeparam>
    /// <returns>
    /// An unordered and unconfigured parallel query of the results.
    /// </returns>
    public static ParallelQuery<TValue> Enumerate<TValue, TInvoke>(
        in ReadOnlySequence<char> csv,
        TInvoke invoker,
        CsvOptions<char>? options = null)
        where TInvoke : ICsvParallelTryInvoke<char, TValue>
    {
        return GetParallelQuery<char, TValue, TInvoke>(options ?? CsvOptions<char>.Default, invoker, in csv);
    }

    /// <inheritdoc cref="Enumerate{TValue,TInvoke}(in System.Buffers.ReadOnlySequence{char},TInvoke,FlameCsv.CsvOptions{char}?)"/>
    public static ParallelQuery<TValue> Enumerate<TValue, TInvoke>(
        in ReadOnlySequence<byte> csv,
        TInvoke invoker,
        CsvOptions<byte>? options = null)
        where TInvoke : ICsvParallelTryInvoke<byte, TValue>
    {
        return GetParallelQuery<byte, TValue, TInvoke>(options ?? CsvOptions<byte>.Default, invoker, in csv);
    }

    /// <summary>
    /// Reads instances of <typeparamref name="TValue"/> from the provided CSV data in parallel.
    /// </summary>
    /// <param name="csv">CSV data</param>
    /// <param name="options">Options-instance</param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns>An unordered parallel query for the records</returns>
    /// <remarks>
    /// Call <see cref="ParallelEnumerable.AsOrdered"/> if you need the records in their original order.
    /// No other parallel options such as merging strategies are set by default, configure them yourself as needed.
    /// </remarks>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static ParallelQuery<TValue> Read<T, TValue>(
        ReadOnlyMemory<T> csv,
        CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        return Read<T, TValue>(new ReadOnlySequence<T>(csv), options);
    }

    /// <summary>
    /// Reads instances of <typeparamref name="TValue"/> from the provided CSV data in parallel.
    /// </summary>
    /// <param name="csv">CSV data</param>
    /// <param name="options">Options-instance</param>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <returns>An unordered parallel query for the records</returns>
    /// <remarks>
    /// Call <see cref="ParallelEnumerable.AsOrdered"/> if you need the records in their original order.
    /// No other parallel options such as merging strategies are set by default, configure them yourself as needed.
    /// </remarks>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    public static ParallelQuery<TValue> Read<T, TValue>(
        in ReadOnlySequence<T> csv,
        CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        options ??= CsvOptions<T>.Default;
        return GetParallelQuery<T, TValue, ValueParallelInvoke<T, TValue>>(
            options,
            ValueParallelInvoke<T, TValue>.Create(options),
            in csv);
    }

    /// <summary>
    /// Reads instances of <typeparamref name="TValue"/> from the provided CSV data in parallel.
    /// </summary>
    /// <param name="csv">CSV data</param>
    /// <param name="typeMap">Type map used for binding</param>
    /// <param name="options">Options-instance</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <typeparam name="TValue">Parsed value</typeparam>
    /// <returns>An unordered parallel query for the records</returns>
    /// <remarks>
    /// Call <see cref="ParallelEnumerable.AsOrdered"/> if you need the records in their original order.
    /// No other parallel options such as merging strategies are set by default, configure them yourself as needed.
    /// </remarks>
    public static ParallelQuery<TValue> Read<T, TValue>(
        ReadOnlyMemory<T> csv,
        CsvTypeMap<T, TValue> typeMap,
        CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        options ??= CsvOptions<T>.Default;
        return GetParallelQuery<T, TValue, ValueParallelInvoke<T, TValue>>(
            options,
            ValueParallelInvoke<T, TValue>.Create(options, typeMap),
            new ReadOnlySequence<T>(csv));
    }

    /// <summary>
    /// Reads instances of <typeparamref name="TValue"/> from the provided CSV data in parallel.
    /// </summary>
    /// <param name="csv">CSV data</param>
    /// <param name="typeMap">Type map used for binding</param>
    /// <param name="options">Options-instance</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <typeparam name="TValue">Parsed value</typeparam>
    /// <returns>An unordered parallel query for the records</returns>
    /// <remarks>
    /// Call <see cref="ParallelEnumerable.AsOrdered"/> if you need the records in their original order.
    /// No other parallel options such as merging strategies are set by default, configure them yourself as needed.
    /// </remarks>
    public static ParallelQuery<TValue> Read<T, TValue>(
        in ReadOnlySequence<T> csv,
        CsvTypeMap<T, TValue> typeMap,
        CsvOptions<T>? options = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(typeMap);
        options ??= CsvOptions<T>.Default;
        return GetParallelQuery<T, TValue, ValueParallelInvoke<T, TValue>>(
            options,
            ValueParallelInvoke<T, TValue>.Create(options, typeMap),
            in csv);
    }

    private static ParallelQuery<TResult> GetParallelQuery<T, TResult, TSelector>(
        CsvOptions<T> options,
        TSelector selector,
        in ReadOnlySequence<T> data)
        where T : unmanaged, IBinaryInteger<T>
        where TSelector : ICsvParallelTryInvoke<T, TResult>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(selector);

        if (!IsSupported(options))
        {
            Throw.NotSupported("Parallel reading is not supported: read-ahead is disabled or dialect is not ASCII");
        }

        if (data.IsEmpty)
        {
            return ParallelEnumerable.Empty<TResult>();
        }

        CsvParser<T> parser = CsvParser.Create(options, CsvPipeReader.Create(in data));
        return Core<T, TResult, TSelector>(parser, selector).AsParallel();
    }

    private static IEnumerable<TResult> Core<T, TResult, TSelector>(
        [HandlesResourceDisposal] CsvParser<T> parser,
        TSelector selector)
        where T : unmanaged, IBinaryInteger<T>
        where TSelector : ICsvParallelTryInvoke<T, TResult>
    {
        // use a separate memory-owner for each thread
        ThreadLocal<BufferFactory<T>> bufferCache = new(
            () => new(parser.Options._memoryPool),
            trackAllValues: true);

        int index = 0;
        bool needsHeader = parser.Options._hasHeader;
        CsvHeader? header = null;

        // while the read-ahead buffer is being drained, we can't more data from the sequence
        // keep track of active operations and spin if needed until they are done
        SpinWait spin = new();
        long activeOperations = 0;

        try
        {
            CsvFields<T> fields;

            while (parser.TryReadUnbuffered(out fields, false))
            {
                do
                {
                    Interlocked.Increment(ref activeOperations);
                    Interlocked.Increment(ref index);

                    Func<int, Span<T>>? getBuffer = fields.NeedsUnescapeBuffer
                        ? bufferCache.Value!.GetBuffer
                        : null;

                    CsvFieldsRef<T> reader = new(in fields, getBuffer: getBuffer!);

                    if (needsHeader)
                    {
                        header = CsvHeader.Parse(parser.Options, ref reader);
                        needsHeader = false;
                        Interlocked.Decrement(ref activeOperations);
                        continue;
                    }

                    CsvParallelState state = new() { Header = header, RecordIndex = index };

                    if (selector.TryInvoke(ref reader, in state, out var result))
                    {
                        yield return result;
                    }

                    Interlocked.Decrement(ref activeOperations);
                } while (parser.TryGetBuffered(out fields));

                while (Interlocked.Read(in activeOperations) != 0)
                {
                    spin.SpinOnce();
                }
            }

            while (Interlocked.Read(in activeOperations) != 0)
            {
                spin.SpinOnce();
            }

            if (parser.TryReadUnbuffered(out fields, isFinalBlock: true))
            {
                index++;

                CsvFieldsRef<T> reader = new(in fields, getBuffer: bufferCache.Value!.GetBuffer);

                // maybe we *only* have a header record without a newline? validate the data and return
                if (needsHeader)
                {
                    _ = CsvHeader.Parse(parser.Options, ref reader);
                    yield break;
                }

                CsvParallelState state = new() { Header = header, RecordIndex = index };

                if (selector.TryInvoke(ref reader, in state, out var result))
                {
                    yield return result;
                }
            }
        }
        finally
        {
            using (parser)
            {
                foreach (var buffer in bufferCache.Values)
                {
                    buffer.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Factory for creating buffers for unescaping.
    /// By default, unescaping is done using the shared buffer in the parser.
    /// </summary>
    private sealed class BufferFactory<T> : IDisposable where T : unmanaged, IEquatable<T>
    {
        public Func<int, Span<T>> GetBuffer { get; }

        private IMemoryOwner<T>? _owner;

        public BufferFactory(MemoryPool<T> pool)
        {
            // TODO: handle multiple unescaped fields!
            // slice the span to ensure exact length
            GetBuffer = length => pool.EnsureCapacity(ref _owner, length).Span;
        }

        public void Dispose() => _owner?.Dispose();
    }
}
