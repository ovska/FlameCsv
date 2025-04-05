using System.Data;
using FlameCsv.Enumeration;
using FlameCsv.Extensions;
using DAMT = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

namespace FlameCsv;

public readonly partial struct CsvValueRecord<T> : IDataRecord
{
    bool IDataRecord.GetBoolean(int i) => ParseField(Options.Aot.GetConverter<bool>(), i);

    byte IDataRecord.GetByte(int i) => ParseField(Options.Aot.GetConverter<byte>(), i);

    long IDataRecord.GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        Span<byte> destination = buffer.AsSpan(bufferoffset, length);
        Memory<byte> bytes = ParseField(Options.Aot.GetConverter<Memory<byte>>(), i);
        ReadOnlySpan<byte> source = bytes.Span.Slice(checked((int)fieldOffset));

        if (source.Length > length)
        {
            source = source.Slice(0, length);
        }

        source.CopyTo(destination);
        return source.Length;
    }

    char IDataRecord.GetChar(int i) => ParseField(Options.Aot.GetConverter<char>(), i);

    long IDataRecord.GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        Span<char> destination = buffer.AsSpan(bufferoffset, length);
        string chars = ParseField(Options.Aot.GetConverter<string>(), i);
        ReadOnlySpan<char> source = chars.AsSpan().Slice(checked((int)fieldoffset));

        if (source.Length > length)
        {
            source = source.Slice(0, length);
        }

        source.CopyTo(destination);
        return source.Length;
    }

    IDataReader IDataRecord.GetData(int i)
    {
        if (_owner is CsvRecordEnumerator<T> enumerator)
        {
            return enumerator;
        }

        throw new NotSupportedException("This record is not owned by a CsvRecordEnumerator.");

    }

    string IDataRecord.GetDataTypeName(int i) => throw new NotSupportedException();

    DateTime IDataRecord.GetDateTime(int i) => ParseField(Options.Aot.GetConverter<DateTime>(), i);

    decimal IDataRecord.GetDecimal(int i) => ParseField(Options.Aot.GetConverter<decimal>(), i);

    double IDataRecord.GetDouble(int i) => ParseField(Options.Aot.GetConverter<double>(), i);

    [return: DAM(DAMT.PublicFields | DAMT.PublicProperties)]
    Type IDataRecord.GetFieldType(int i) => throw new NotSupportedException();

    float IDataRecord.GetFloat(int i) => ParseField(Options.Aot.GetConverter<float>(), i);
    Guid IDataRecord.GetGuid(int i) => ParseField(Options.Aot.GetConverter<Guid>(), i);
    short IDataRecord.GetInt16(int i) => ParseField(Options.Aot.GetConverter<short>(), i);
    int IDataRecord.GetInt32(int i) => ParseField(Options.Aot.GetConverter<int>(), i);
    long IDataRecord.GetInt64(int i) => ParseField(Options.Aot.GetConverter<long>(), i);

    string IDataRecord.GetName(int i)
    {
        _owner.EnsureVersion(_version);
        if (_owner.Header is null) Throw.NotSupported_CsvHasNoHeader();
        return _owner.Header.Values[i];
    }

    int IDataRecord.GetOrdinal(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _owner.EnsureVersion(_version);
        if (_owner.Header is null) Throw.NotSupported_CsvHasNoHeader();
        return _owner.Header.TryGetValue(name, out int ordinal) ? ordinal : -1;
    }

    string IDataRecord.GetString(int i)
    {
        return Options.GetAsString(GetField(i));
    }

    object IDataRecord.GetValue(int i)
    {
        return Options.GetAsString(GetField(i));
    }

    int IDataRecord.GetValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length < FieldCount)
        {
            throw new ArgumentException(
                $"The array is too small to hold the values. Expected {FieldCount} but got {values.Length}.");
        }

        int i = 0;

        foreach (var value in this)
        {
            values[i++] = Options.GetAsString(value);
        }

        return i;
    }

    bool IDataRecord.IsDBNull(int i)
    {
        var value = GetField(i);
        return value.IsEmpty || value.SequenceEqual(Options.GetNullToken(null).Span);
    }

    object IDataRecord.this[int i] => Options.GetAsString(GetField(i));
    object IDataRecord.this[string name] => Options.GetAsString(GetField(name));
}
