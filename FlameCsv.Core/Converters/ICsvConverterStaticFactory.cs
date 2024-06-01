namespace FlameCsv.Converters;

public interface ICsvConverterStaticFactory<T, TValue, TOptions>
    where T : unmanaged, IEquatable<T>
    where TOptions : CsvOptions<T>
{
    abstract CsvConverter<T, TValue> Create(TOptions options);
}
