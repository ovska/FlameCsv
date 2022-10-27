using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace FlameCsv.Readers;

internal sealed class CsvStreamReader<TValue>
{
    private readonly CsvConfiguration<byte> _configuration;

    public CsvStreamReader(CsvConfiguration<byte> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    public IAsyncEnumerable<TValue> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ReadAsync(PipeReader.Create(stream), cancellationToken);
    }

    public async IAsyncEnumerable<TValue> ReadAsync(
        PipeReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        using var processor = new CsvHeaderProcessor<byte, TValue>(_configuration);

        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                while (processor.TryContinueRead(ref buffer, out TValue value))
                {
                    yield return value;
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    // Read leftover data if there was no final newline
                    if (!buffer.IsEmpty && processor.TryContinueRead(ref buffer, out TValue value))
                    {
                        yield return value;
                    }

                    break;
                }
            }
        }
        finally
        {
            await reader.CompleteAsync();
        }
    }
}
