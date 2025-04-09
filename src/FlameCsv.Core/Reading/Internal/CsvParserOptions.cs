namespace FlameCsv.Reading.Internal;

internal readonly struct CsvParserOptions<T> where T : unmanaged
{
    public Allocator<T>? MultiSegmentAllocator { get; init; }
    public Allocator<T>? UnescapeAllocator { get; init; }
}
