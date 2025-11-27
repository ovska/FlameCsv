using System.Buffers;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public readonly partial struct CsvRecord<T> : IDataRecord
{
    bool IDataRecord.GetBoolean(int i) => ParseField(Options.Aot.GetConverter<bool>(), i);

    byte IDataRecord.GetByte(int i) => ParseField(Options.Aot.GetConverter<byte>(), i);

    long IDataRecord.GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
    {
        Span<byte> destination = buffer.AsSpan(bufferoffset, length);

        ReadOnlySpan<T> value = GetField(i).Slice((int)fieldOffset);

        if (typeof(T) == typeof(byte))
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<T, byte>(value);

            if (bytes.Length > length)
            {
                bytes = bytes.Slice(0, length);
            }

            bytes.CopyTo(destination);
            return bytes.Length;
        }

        if (typeof(T) == typeof(char))
        {
            ReadOnlySpan<char> chars = MemoryMarshal.Cast<T, char>(value);

            var status = Utf8.FromUtf16(
                chars,
                destination,
                out _,
                out int bytesWritten,
                replaceInvalidSequences: true,
                isFinalBlock: true
            );

            return status switch
            {
                OperationStatus.Done or OperationStatus.DestinationTooSmall => bytesWritten,
                _ => throw new InvalidDataException($"Cannot convert field {i} to bytes"),
            };
        }

        throw Token<T>.NotSupported;
    }

    char IDataRecord.GetChar(int i) => ParseField(Options.Aot.GetConverter<char>(), i);

    long IDataRecord.GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
    {
        Span<char> destination = buffer.AsSpan(bufferoffset, length);
        ReadOnlySpan<T> field = GetField(i).Slice((int)fieldoffset);

        if (typeof(T) == typeof(char))
        {
            ReadOnlySpan<char> chars = MemoryMarshal.Cast<T, char>(field);

            if (chars.Length > length)
            {
                chars = chars.Slice(0, length);
            }

            chars.CopyTo(destination);
            return chars.Length;
        }

        if (typeof(T) == typeof(byte))
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<T, byte>(field);

            var status = Utf8.ToUtf16(
                bytes,
                destination,
                out _,
                out int charsWritten,
                replaceInvalidSequences: true,
                isFinalBlock: true
            );

            return status switch
            {
                OperationStatus.Done or OperationStatus.DestinationTooSmall => charsWritten,
                _ => throw new InvalidDataException($"Cannot convert field {i} to chars"),
            };
        }

        throw Token<T>.NotSupported;
    }

    IDataReader IDataRecord.GetData(int i)
    {
        throw new NotSupportedException("CSV records do not support nested data readers.");
    }

    string IDataRecord.GetDataTypeName(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, FieldCount);
        return "System.String";
    }

    DateTime IDataRecord.GetDateTime(int i) => ParseField(Options.Aot.GetConverter<DateTime>(), i);

    decimal IDataRecord.GetDecimal(int i) => ParseField(Options.Aot.GetConverter<decimal>(), i);

    double IDataRecord.GetDouble(int i) => ParseField(Options.Aot.GetConverter<double>(), i);

    float IDataRecord.GetFloat(int i) => ParseField(Options.Aot.GetConverter<float>(), i);

    Guid IDataRecord.GetGuid(int i) => ParseField(Options.Aot.GetConverter<Guid>(), i);

    short IDataRecord.GetInt16(int i) => ParseField(Options.Aot.GetConverter<short>(), i);

    int IDataRecord.GetInt32(int i) => ParseField(Options.Aot.GetConverter<int>(), i);

    long IDataRecord.GetInt64(int i) => ParseField(Options.Aot.GetConverter<long>(), i);

    string IDataRecord.GetName(int i)
    {
        _owner.EnsureVersion(_version);
        if (_owner.Header is null)
        {
            Throw.NotSupported_CsvHasNoHeader();
        }
        return _owner.Header.Values[i];
    }

    int IDataRecord.GetOrdinal(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _owner.EnsureVersion(_version);
        if (_owner.Header is null)
        {
            Throw.NotSupported_CsvHasNoHeader();
        }
        return _owner.Header.TryGetValue(name, out int ordinal) ? ordinal : -1;
    }

    string IDataRecord.GetString(int i)
    {
        return Transcode.ToString(GetField(i));
    }

    object IDataRecord.GetValue(int i)
    {
        return Transcode.ToString(GetField(i));
    }

    int IDataRecord.GetValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var record = new CsvRecordRef<T>(in _slice);

        int i = 0;
        int end = Math.Min(values.Length, record.FieldCount);

        for (; i < end; i++)
        {
            values[i] = Transcode.ToString(record[i]);
        }

        return i;
    }

    bool IDataRecord.IsDBNull(int i)
    {
        ReadOnlySpan<T> value = GetField(i);
        ReadOnlySpan<T> nullToken = Options.DefaultNullToken;

        // common case
        if (nullToken.IsEmpty)
        {
            return value.IsEmpty;
        }

        return value.SequenceEqual(nullToken);
    }

    object IDataRecord.this[int i] => Transcode.ToString(GetField(i));
    object IDataRecord.this[string name] => Transcode.ToString(GetField(name));

    [return: DAM(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
    Type IDataRecord.GetFieldType(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, FieldCount);
        return typeof(string);
    }
}
