#if FEATURE_PARALLEL
using System.Buffers;
using FlameCsv.Reading;
using FlameCsv.Reading.Parallel;

namespace FlameCsv;

public static partial class CsvParallel
{
    private readonly struct ForEachInvoker<T>(Action<CsvFieldsRef<T>, CsvParallelState> action)
        : ICsvParallelTryInvoke<T, object?>
        where T : unmanaged, IBinaryInteger<T>
    {
        public bool TryInvoke(
            scoped ref CsvFieldsRef<T> fields,
            in CsvParallelState state,
            out object? result)
        {
            action(fields, state);
            result = null;
            return true;
        }
    }

    public static void ForEach(
        string? csv,
        Action<CsvFieldsRef<char>, CsvParallelState> action,
        CsvOptions<char>? options = null,
        ParallelOptions? parallelOptions = null)
    {
        ForEachCore(CsvPipeReader.Create(csv), action, options, parallelOptions);
    }

    public static void ForEach(
        ReadOnlyMemory<char> csv,
        Action<CsvFieldsRef<char>, CsvParallelState> action,
        CsvOptions<char>? options = null,
        ParallelOptions? parallelOptions = null)
    {
        ForEachCore(CsvPipeReader.Create(csv), action, options, parallelOptions);
    }

    public static void ForEach(
        in ReadOnlySequence<char> csv,
        Action<CsvFieldsRef<char>, CsvParallelState> action,
        CsvOptions<char>? options = null,
        ParallelOptions? parallelOptions = null)
    {
        ForEachCore(CsvPipeReader.Create(in csv), action, options, parallelOptions);
    }

    public static void ForEach(
        TextReader textReader,
        Action<CsvFieldsRef<char>, CsvParallelState> action,
        CsvOptions<char>? options = null,
        CsvReaderOptions readerOptions = default,
        ParallelOptions? parallelOptions = null)
    {
        options ??= CsvOptions<char>.Default;
        ForEachCore(
            CsvPipeReader.Create(textReader, options._memoryPool, readerOptions),
            action,
            options,
            parallelOptions);
    }

    private static void ForEachCore<T>(
        ICsvPipeReader<T> reader,
        Action<CsvFieldsRef<T>, CsvParallelState> action,
        CsvOptions<T>? options = null,
        ParallelOptions? parallelOptions = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(action);

        Parallel.ForEach(
            Core(CsvParser.Create(options ?? CsvOptions<T>.Default, reader)),
            (iter, state) =>
            {
                CsvFields<T> fields = iter.Fields;
                CsvFieldsRef<T> fieldsRef = new(in fields, iter.GetBuffer);
                action(fieldsRef, new CsvParallelState { Index = iter.Index, Header = iter.Header, LoopState = state });
            });
    }
}
#endif
