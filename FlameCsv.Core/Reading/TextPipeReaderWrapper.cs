using System.Runtime.CompilerServices;

namespace FlameCsv.Reading;

internal readonly struct TextPipeReaderWrapper : ICsvPipeReader<char>
{
    private readonly TextPipeReader _reader;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextPipeReaderWrapper(TextPipeReader reader) => _reader = reader;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AdvanceTo(SequencePosition consumed, SequencePosition examined)
    {
        _reader.AdvanceTo(consumed, examined);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask DisposeAsync()
    {
        return _reader.DisposeAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<CsvReadResult<char>> ReadAsync(CancellationToken cancellationToken = default)
    {
        return _reader.ReadAsync(cancellationToken);
    }
}
