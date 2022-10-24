using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;

namespace FlameCsv.Readers;

internal sealed class CsvStreamReader<TValue>
{
    private readonly CsvConfiguration<byte> _configuration;
    private readonly CsvBindingCollection<TValue> _bindings;

    public CsvStreamReader(
        CsvConfiguration<byte> configuration,
        CsvBindingCollection<TValue> bindings)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(bindings);
        _configuration = configuration;
        _bindings = bindings;
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

        using var processor = new CsvProcessor<byte, DoubleEscapeReader<byte>, TValue>(_configuration, _bindings);

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
