namespace FlameCsv.Reading.Internal;

internal abstract class CsvScalarTokenizer<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Reads fields from the data into <paramref name="destination"/> until the end of the data is reached.
    /// </summary>
    /// <param name="destination">Buffer to parse the records to</param>
    /// <param name="startIndex">Start index in the data</param>
    /// <param name="data">Data to read from</param>
    /// <param name="readToEnd">Whether to read to end even if data has no trailing newline</param>
    /// <returns>Number of fields read</returns>
    public abstract int Tokenize(Span<uint> destination, int startIndex, ReadOnlySpan<T> data, bool readToEnd);
}
