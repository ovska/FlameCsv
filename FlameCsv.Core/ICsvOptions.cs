namespace FlameCsv;

public interface ICsvOptions<T> where T : unmanaged, IEquatable<T>
{
    CsvTokens<T> Tokens { get; }
    SecurityLevel Security { get; }
}
