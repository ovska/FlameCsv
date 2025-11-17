namespace FlameCsv.Reading.Internal;

internal interface IRecordOwner
{
    void EnsureVersion(int version);
    CsvHeader? Header { get; }
    IDictionary<object, object> MaterializerCache { get; }
}
