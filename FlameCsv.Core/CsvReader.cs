using FlameCsv.Reading;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace FlameCsv;

/// <summary>
/// Provides static methods for reading CSV records as objects or structs.
/// </summary>
public static partial class CsvReader
{
    private static async IAsyncEnumerable<TValue> ReadCoreAsync<T, TValue, TReader, TProcessor>(
        TReader reader,
        TProcessor processor,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where T : unmanaged, IEquatable<T>
        where TReader : ICsvPipeReader<T>
        where TProcessor : struct, ICsvProcessor<T, TValue>
    {
        try
        {
            while (true)
            {
                CsvReadResult<T> result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<T> buffer = result.Buffer;

                // Slice out the records line-by-line from the buffer until we reach the end
                // or there are no more newlines
                while (processor.TryRead(ref buffer, out TValue value, isFinalBlock: false))
                {
                    yield return value;
                }

                // Signal how much was read from the buffer
                reader.AdvanceTo(buffer.Start, buffer.End);

                // No more data to read
                if (result.IsCompleted)
                {
                    // Try to read leftover data, in case there was no final newline
                    if (processor.TryRead(ref buffer, out TValue value, isFinalBlock: true))
                    {
                        yield return value;
                    }

                    break;
                }

                // No more newlines, but reader wasn't completed, keep reading
            }
        }
        finally
        {
            processor.Dispose();
            await reader.DisposeAsync();
        }
    }

    private static IEnumerable<TValue> ReadCore<T, TValue, TProcessor>(
        ReadOnlySequence<T> buffer,
        TProcessor processor)
        where T : unmanaged, IEquatable<T>
        where TProcessor : struct, ICsvProcessor<T, TValue>
    {
        try
        {
            TValue value;

            while (processor.TryRead(ref buffer, out value, isFinalBlock: false))
            {
                yield return value;
            }

            // try to read final record if buffer didn't have trailing newline
            if (processor.TryRead(ref buffer, out value, isFinalBlock: true))
            {
                yield return value;
            }
        }
        finally
        {
            processor.Dispose();
        }
    }
}
