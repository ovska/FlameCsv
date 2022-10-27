using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Binding.Providers;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Readers;

public static partial class CsvReader
{
    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        CsvConfiguration<char> configuration,
        Stream stream,
        Encoding? encoding = null,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(stream);
        Guard.CanRead(stream);

        return ReadAsync<TValue>(
            configuration,
            new StreamReader(stream, encoding: encoding, leaveOpen: leaveOpen, bufferSize: 4096),
            leaveOpen,
            cancellationToken);
    }

    public static IAsyncEnumerable<TValue> ReadAsync<TValue>(
        CsvConfiguration<char> configuration,
        TextReader textReader,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(textReader);

        if (configuration.BindingProvider is ICsvHeaderBindingProvider<char>)
        {
            return ReadAsyncInternal<TValue, CsvHeaderProcessor<char, TValue>>(
                textReader,
                new CsvHeaderProcessor<char, TValue>(configuration),
                leaveOpen,
                cancellationToken);
        }

        return ReadAsyncInternal<TValue, CsvProcessor<char, TValue>>(
            textReader,
            new CsvProcessor<char, TValue>(configuration),
            leaveOpen,
            cancellationToken);
    }

    private static async IAsyncEnumerable<TValue> ReadAsyncInternal<TValue, TProcessor>(
        TextReader textReader,
        TProcessor processor,
        bool leaveOpen,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TProcessor : struct, ICsvProcessor<char, TValue>
    {
        using var reader = new TextPipeReader(textReader, 4096, leaveOpen);

        using (processor)
        {
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
    }
}
