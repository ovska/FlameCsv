using System.Data;
using DAMT = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

namespace FlameCsv.Enumeration;

public sealed partial class CsvRecordEnumerator<T> : IDataReader
{
    void IDataReader.Close() => Dispose();

    DataTable IDataReader.GetSchemaTable()
    {
        throw new NotSupportedException("Schema table is not currently supported.");
    }

    bool IDataReader.NextResult() => MoveNext();

    bool IDataReader.Read() => MoveNext();

    int IDataReader.Depth => 0;

    bool IDataReader.IsClosed => _version == -1;

    int IDataReader.RecordsAffected => -1;

    #region Datarecord

    // @formatter:off
    IDataReader IDataRecord.GetData(int i) => this; // TODO: what to do here?
    int IDataRecord.FieldCount => _current.FieldCount;
    bool IDataRecord.GetBoolean(int i) => ((IDataRecord)_current).GetBoolean(i);
    byte IDataRecord.GetByte(int i) => ((IDataRecord)_current).GetByte(i);
    long IDataRecord.GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => ((IDataRecord)_current).GetBytes(i, fieldOffset, buffer, bufferoffset, length);
    char IDataRecord.GetChar(int i) => ((IDataRecord)_current).GetChar(i);
    long IDataRecord.GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => ((IDataRecord)_current).GetChars(i, fieldoffset, buffer, bufferoffset, length);
    string IDataRecord.GetDataTypeName(int i) => ((IDataRecord)_current).GetDataTypeName(i);
    DateTime IDataRecord.GetDateTime(int i) => ((IDataRecord)_current).GetDateTime(i);
    decimal IDataRecord.GetDecimal(int i) => ((IDataRecord)_current).GetDecimal(i);
    double IDataRecord.GetDouble(int i) => ((IDataRecord)_current).GetDouble(i);
    [return: DAM(DAMT.PublicFields | DAMT.PublicProperties)] Type IDataRecord.GetFieldType(int i) => ((IDataRecord)_current).GetFieldType(i);
    float IDataRecord.GetFloat(int i) => ((IDataRecord)_current).GetFloat(i);
    Guid IDataRecord.GetGuid(int i) => ((IDataRecord)_current).GetGuid(i);
    short IDataRecord.GetInt16(int i) => ((IDataRecord)_current).GetInt16(i);
    int IDataRecord.GetInt32(int i) => ((IDataRecord)_current).GetInt32(i);
    long IDataRecord.GetInt64(int i) => ((IDataRecord)_current).GetInt64(i);
    string IDataRecord.GetName(int i) => ((IDataRecord)_current).GetName(i);
    int IDataRecord.GetOrdinal(string name) => ((IDataRecord)_current).GetOrdinal(name);
    string IDataRecord.GetString(int i) => ((IDataRecord)_current).GetString(i);
    object IDataRecord.GetValue(int i) => ((IDataRecord)_current).GetValue(i);
    int IDataRecord.GetValues(object[] values) => ((IDataRecord)_current).GetValues(values);
    bool IDataRecord.IsDBNull(int i) => ((IDataRecord)_current).IsDBNull(i);
    object IDataRecord.this[int i] => ((IDataRecord)_current)[i];
    object IDataRecord.this[string name] => ((IDataRecord)_current)[name];
    // @formatter:on

    #endregion
}
