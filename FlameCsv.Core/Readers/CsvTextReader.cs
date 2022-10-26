using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

internal sealed class CsvTextReader<TValue> : IDisposable
{
    private readonly TextReader _innerReader;
    private readonly CsvConfiguration<char> _configuration;
    private readonly int _bufferSize;

    internal CsvTextReader(
        Stream stream,
        Encoding encoding,
        CsvConfiguration<char> configuration,
        int bufferSize = 8192)
        : this(new StreamReader(stream, encoding, bufferSize: bufferSize), configuration, bufferSize)
    {
    }

    internal CsvTextReader(
        TextReader innerReader,
        CsvConfiguration<char> configuration,
        int bufferSize = 8192)
    {
        _innerReader = innerReader;
        _configuration = configuration;
        _bufferSize = bufferSize;
        Guard.IsGreaterThan(bufferSize, 0);
    }

    public async IAsyncEnumerable<TValue> ReadAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new TextPipeReader(_innerReader, _bufferSize);
        using var processor = new CsvProcessor<char, DoubleEscapeReader<char>, TValue>(_configuration);

        while (true)
        {
            TextReadResult result = await reader.ReadAsync(cancellationToken);
            ReadOnlySequence<char> buffer = result.Buffer;

            while (processor.TryContinueRead(ref buffer, out TValue value))
            {
                yield return value;
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                // Read leftover data if there was no final newline
                if (!buffer.IsEmpty && processor.TryContinueRead(ref buffer, out TValue value2))
                {
                    yield return value2;
                }

                break;
            }
        }
    }

    public void Dispose()
    {
        _innerReader.Dispose();
    }
}
