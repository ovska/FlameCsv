namespace FlameCsv.Reading.Internal;

internal readonly ref struct FieldBuffer
{
    public required Span<uint> Fields { get; init; }
    public required Span<byte> Quotes { get; init; }
}
