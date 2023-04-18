namespace FlameCsv.Configuration;

public interface ICsvHeaderConfiguration<T> where T : unmanaged, IEquatable<T>
{
    IReadOnlyDictionary<string, int> CreateHeaderDictionary(CsvRecord<T> record);
    bool Matches(ReadOnlySpan<T> tokens, ReadOnlySpan<char> chars);
}
