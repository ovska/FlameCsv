#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace FlameCsv;

using IO;
#if FEATURE_PARALLEL
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Reading;
using Reading.Internal;
using JetBrains.Annotations;

/// <summary>
/// Provides methods for parallel processing of CSV data.
/// </summary>
[PublicAPI]
public static class CsvParallel
{
    private static ParallelOptions DefaultParallelOptions { get; } = new();

    public static ParallelLoopResult ForEach<T>(
        ICsvPipeReader<T> reader,
        Action<CsvValueRecord<T>> action,
        CsvOptions<T>? options = null,
        ParallelOptions? parallelOptions = null
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(action);

        options ??= CsvOptions<T>.Default;
        parallelOptions ??= DefaultParallelOptions;

        RecordTracker tracker = new();

        return Parallel.ForEach(
            GetParallelQuery(options, reader, tracker, parallelOptions.CancellationToken),
            parallelOptions,
            record =>
            {
                try
                {
                    action(record);
                }
                finally
                {
                    tracker.ReleaseRecord();
                }
            }
        );
    }

    public static Task ForEachAsync<T>(
        ICsvPipeReader<T> reader,
        Func<CsvValueRecord<T>, CancellationToken, ValueTask> action,
        CsvOptions<T>? options = null,
        ParallelOptions? parallelOptions = null
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(action);

        options ??= CsvOptions<T>.Default;
        parallelOptions ??= DefaultParallelOptions;

        RecordTracker tracker = new();

        return Parallel.ForEachAsync(
            GetParallelQueryAsync(options, reader, tracker, parallelOptions.CancellationToken),
            parallelOptions,
            async (record, ct) =>
            {
                try
                {
                    await action(record, ct).ConfigureAwait(false);
                }
                finally
                {
                    tracker.ReleaseRecord();
                }
            }
        );
    }

    internal static IEnumerable<CsvValueRecord<T>> GetParallelQuery<T>(
        CsvOptions<T> options,
        ICsvPipeReader<T> reader,
        [HandlesResourceDisposal] RecordTracker trackerArg,
        CancellationToken cancellationToken
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);

        using var tracker = trackerArg;
        using var slabAllocator = new SlabAllocator<T>(options.Allocator);

        using CsvParser<T> parser = CsvParser.CreateCore(
            options,
            reader,
            new CsvParserOptions<T> { UnescapeAllocator = slabAllocator, MultiSegmentAllocator = slabAllocator }
        );

        using ParallelEnumerationOwner owner = new();

        int line = 0;
        long position = 0;
        bool needsHeader = parser.Options.HasHeader;

        CsvFields<T> fields;
        CsvValueRecord<T> record;

        while (parser.TryAdvanceReader())
        {
            if (!parser.TryReadLine(out fields, isFinalBlock: false))
            {
                continue;
            }

            if (TryYield(in fields, out record))
            {
                yield return record;
            }

            while (parser.TryGetBuffered(out fields))
            {
                if (TryYield(in fields, out record))
                {
                    yield return record;
                }
            }

            owner.NextVersion();
            slabAllocator.Reset();
        }

        // read the final block
        while (parser.TryReadUnbuffered(out fields, isFinalBlock: true))
        {
            if (TryYield(in fields, out record))
            {
                yield return record;
            }
        }

        bool TryYield(in CsvFields<T> fieldsLocal, out CsvValueRecord<T> recordLocal)
        {
            bool retVal = false;
            Interlocked.Increment(ref line);

            if (needsHeader)
            {
                CsvFieldsRef<T> fieldsRef = new(in fieldsLocal, parser._unescapeAllocator);

                owner.Header = CsvHeader.Parse(
                    parser.Options,
                    ref fieldsRef,
                    options.Comparer,
                    static (comparer, headers) => new CsvHeader(comparer, headers)
                );

                needsHeader = false;
                recordLocal = default;
            }
            else
            {
                tracker.AddRecord();
                recordLocal = new CsvValueRecord<T>(version: 0, position, line, in fields, options, owner);
                retVal = true;
            }

            Interlocked.Add(ref position, fields.GetRecordLength(includeTrailingNewline: true));
            return retVal;
        }
    }

    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task")]
    internal static async IAsyncEnumerable<CsvValueRecord<T>> GetParallelQueryAsync<T>(
        CsvOptions<T> options,
        ICsvPipeReader<T> reader,
        [HandlesResourceDisposal] RecordTracker trackerArg,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);

        using var tracker = trackerArg;
        using var slabAllocator = new SlabAllocator<T>(options.Allocator);

        await using CsvParser<T> parser = CsvParser.CreateCore(
            options,
            reader,
            new CsvParserOptions<T> { UnescapeAllocator = slabAllocator, MultiSegmentAllocator = slabAllocator }
        );

        using ParallelEnumerationOwner owner = new();

        int line = 0;
        long position = 0;
        bool needsHeader = parser.Options.HasHeader;

        CsvFields<T> fields;
        CsvValueRecord<T> record;

        while (await parser.TryAdvanceReaderAsync(cancellationToken))
        {
            if (!parser.TryReadLine(out fields, isFinalBlock: false))
            {
                continue;
            }

            if (TryYield(in fields, out record))
            {
                yield return record;

                // wait until the previous record is released - we can't proceed until the unbuffered record is handled
                await tracker.WaitForAllRecordsReleasedAsync(cancellationToken);
            }

            while (parser.TryGetBuffered(out fields))
            {
                if (TryYield(in fields, out record))
                {
                    yield return record;
                }
            }

            await tracker.WaitForAllRecordsReleasedAsync(cancellationToken);
            owner.NextVersion();
            slabAllocator.Reset();
        }

        // read the final block
        while (parser.TryReadUnbuffered(out fields, isFinalBlock: true))
        {
            if (TryYield(in fields, out record))
            {
                yield return record;
            }
        }

        bool TryYield(in CsvFields<T> fieldsLocal, out CsvValueRecord<T> recordLocal)
        {
            bool retVal = false;
            Interlocked.Increment(ref line);

            if (needsHeader)
            {
                CsvFieldsRef<T> fieldsRef = new(in fieldsLocal, parser._unescapeAllocator);

                owner.Header = CsvHeader.Parse(
                    parser.Options,
                    ref fieldsRef,
                    options.Comparer,
                    static (comparer, headers) => new CsvHeader(comparer, headers)
                );

                needsHeader = false;
                recordLocal = default;
            }
            else
            {
                tracker.AddRecord();
                recordLocal = new CsvValueRecord<T>(version: 0, position, line, in fields, options, owner);
                retVal = true;
            }

            Interlocked.Add(ref position, fields.GetRecordLength(includeTrailingNewline: true));
            return retVal;
        }
    }
}

internal sealed class RecordTracker : IDisposable
{
    private long _activeRecordCount;
    private readonly SemaphoreSlim _allCompleted = new(initialCount: 1, maxCount: 1);

    public void AddRecord()
    {
        if (Interlocked.Increment(ref _activeRecordCount) == 1)
        {
            // First active record - acquire the semaphore to block completion signaling
            _allCompleted.Wait(0);
        }
    }

    public void ReleaseRecord()
    {
        if (Interlocked.Decrement(ref _activeRecordCount) == 0)
        {
            // Last record completed - signal completion
            _allCompleted.Release();
        }
    }

    public void WaitForAllRecordsReleased(CancellationToken cancellationToken)
    {
        if (Interlocked.Read(ref _activeRecordCount) > 0)
        {
            _allCompleted.Wait(cancellationToken);
            // Don't release here - next AddRecord will check and acquire if needed
            _allCompleted.Release();
        }
    }

    public async ValueTask WaitForAllRecordsReleasedAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Read(ref _activeRecordCount) > 0)
        {
            await _allCompleted.WaitAsync(cancellationToken).ConfigureAwait(false);
            _allCompleted.Release();
        }
    }

    public void Dispose() => _allCompleted.Dispose();
}
#else
// ReSharper disable All
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal static class CsvParallel
{
    private static ParallelOptions DefaultParallelOptions { get; } = new();

    public static ParallelLoopResult ForEach<T>(
        ICsvBufferReader<T> reader,
        Action<CsvRecord<T>> action,
        CsvOptions<T>? options = null,
        ParallelOptions? parallelOptions = null
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        throw new NotSupportedException();
    }

    public static Task ForEachAsync<T>(
        ICsvBufferReader<T> reader,
        Func<CsvRecord<T>, CancellationToken, ValueTask> action,
        CsvOptions<T>? options = null,
        ParallelOptions? parallelOptions = null
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        throw new NotSupportedException();
    }
}
#endif
