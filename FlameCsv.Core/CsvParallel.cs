// #if FEATURE_PARALLEL

using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Provides methods for parallel processing of CSV data.
/// </summary>
[PublicAPI]
public static class CsvParallel
{
    internal static IEnumerable<TResult> GetParallelQuery<T, TResult>(
        CsvOptions<T> options,
        ICsvPipeReader<T> reader2,
        Func<CsvValueRecord<T>, TResult> selector)
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader2);

        using var allocator = new SlabAllocator<T>(options.Allocator);

        using CsvParser<T> parser = CsvParser.CreateCore(
            options,
            reader2,
            new CsvParserOptions<T> { UnescapeAllocator = allocator, MultiSegmentAllocator = allocator });

        using ParallelEnumerationOwner owner = new();
        using CancellationTokenSource cts = new();
        using SemaphoreSlim semaphore = new(1, 1);

        int version = owner.Version;
        int line = 0;
        long position = 0;
        bool needsHeader = parser.Options.HasHeader;

        CsvFields<T> fields;

        while (parser.TryAdvanceReader())
        {
            using var scope = semaphore.Lock(cts.Token);

            while (parser.TryReadUnbuffered(out fields, false))
            {
                do
                {
                    Interlocked.Increment(ref line);

                    if (needsHeader)
                    {
                        CsvFieldsRef<T> fieldsRef = new(in fields, parser._unescapeAllocator);

                        owner.Header = CsvHeader.Parse(
                            parser.Options,
                            ref fieldsRef,
                            options.Comparer,
                            static (comparer, headers) => new CsvHeader(comparer, headers));

                        needsHeader = false;
                        continue;
                    }

                    TResult result;

                    try
                    {
                        result = selector(new CsvValueRecord<T>(version, position, line, in fields, options, owner));
                    }
                    catch
                    {
                        cts.Cancel();
                        throw;
                    }

                    Interlocked.Add(ref position, fields.GetRecordLength(includeTrailingNewline: true));

                    yield return result;
                } while (parser.TryGetBuffered(out fields));
            }

            // increment version for every read
            version = owner.NextVersion();
            allocator.Reset();
        }

        // read the final block
        while (parser.TryReadUnbuffered(out fields, isFinalBlock: true))
        {
            line++;

            // maybe we *only* have a header record without a newline? validate the data and return
            if (needsHeader)
            {
                CsvFieldsRef<T> fieldsRef = new(in fields, parser._unescapeAllocator);

                _ = CsvHeader.Parse(
                    parser.Options,
                    ref fieldsRef,
                    options.Comparer,
                    static (comparer, headers) => new CsvHeader(comparer, headers));
                yield break;
            }

            yield return selector(new CsvValueRecord<T>(version, position, line, in fields, options, owner));

            Interlocked.Add(ref position, fields.GetRecordLength(includeTrailingNewline: true));
        }
    }

#if false
    public static IAsyncEnumerable<TResult> Test<T, TResult>(
        in ReadOnlySequence<T> csv,
        CsvOptions<T>? options = null,
        CancellationToken cancellationToken = default)
        where T : unmanaged, IBinaryInteger<T>
    {
        return CoreAsync<T, TResult, ValueParallelInvoke<T, TResult>>(
            CsvParser.Create(options ?? CsvOptions<T>.Default, CsvPipeReader.Create(csv)),
            ValueParallelInvoke<T, TResult>.Create(options),
            cancellationToken);
    }

    private static async IAsyncEnumerable<TResult> CoreAsync<T, TResult, TSelector>(
        [HandlesResourceDisposal] CsvParser<T> parser,
        TSelector selector,
        [EnumeratorCancellation] CancellationToken cancellationToken)
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

        // Semaphore to control the number of concurrent operations
        SemaphoreSlim semaphore = new(0);
        long activeOperations = 0;

        try
        {
            CsvFields<T> fields;

            while (await parser.TryAdvanceReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (Interlocked.Read(ref activeOperations) != 0)
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                while (parser.TryReadUnbuffered(out fields, false))
                {
                    do
                    {
                        Interlocked.Increment(ref index);
                        Interlocked.Increment(ref activeOperations);

                        CsvFieldsRef<T> reader = new(in fields, getBuffer: bufferCache.Value!.GetBuffer);

                        if (needsHeader)
                        {
                            header = CsvHeader.Parse(parser.Options, ref reader);
                            needsHeader = false;
                            Interlocked.Decrement(ref activeOperations);
                            semaphore.Release();
                            continue;
                        }

                        CsvParallelState state = new() { Header = header, Index = index };

                        if (selector.TryInvoke(ref reader, in state, out var result))
                        {
                            yield return result;
                        }

                        Interlocked.Decrement(ref activeOperations);
                        semaphore.Release();
                    } while (parser.TryGetBuffered(out fields));
                }
            }

            // reader cannot be advanced anymore, wait until all previous reads are done
            while (Interlocked.Read(ref activeOperations) != 0)
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            // read the final block
            while (parser.TryReadUnbuffered(out fields, isFinalBlock: true))
            {
                index++;

                CsvFieldsRef<T> reader = new(in fields, getBuffer: bufferCache.Value!.GetBuffer);

                // maybe we *only* have a header record without a newline? validate the data and return
                if (needsHeader)
                {
                    _ = CsvHeader.Parse(parser.Options, ref reader);
                    yield break;
                }

                CsvParallelState state = new() { Header = header, Index = index };

                if (selector.TryInvoke(ref reader, in state, out var result))
                {
                    yield return result;
                }

                semaphore.Release();
            }
        }
        finally
        {
            await using (parser)
            {
                foreach (var buffer in bufferCache.Values) buffer.Dispose();
            }
        }
    }
#endif
}
// #endif
