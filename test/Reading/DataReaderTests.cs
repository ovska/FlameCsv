using System.Data;
using System.Text;
using FlameCsv.Enumeration;

namespace FlameCsv.Tests.Reading;

public class DataReaderTests
{
    const string data =
        "id,name,age,height,weight,guid,gender,isactive\n"
        + "1,John Doe,30,180.5,75.0,0123e456-7890-1234-5678-123456789012,M,true\n"
        + "2,Jane Smith,25,165.0,60.0,12345678-90ab-cdef-1234-567890abcdef,F,false\n";

    [Fact]
    public void Should_Throw_If_No_Header()
    {
        using CsvRecordEnumerator<char> enumerator = CsvReader
            .Enumerate(data, new() { HasHeader = false })
            .GetEnumerator();

        IDataReader reader = enumerator;

        Assert.True(reader.Read());

        Assert.Throws<NotSupportedException>(() => reader.GetName(0));
        Assert.Throws<NotSupportedException>(() => reader.GetOrdinal("id"));
    }

    [Fact]
    public void Should_Parse_DbNull()
    {
        // default null value is empty string
        TestImpl(
            CsvReader.Enumerate("id,col,name\n1,,John Doe").GetEnumerator(),
            r => ((CsvRecordEnumerator<char>)r).Current
        );
        TestImpl(
            CsvReader.Enumerate("id,col,name\n1,,John Doe"u8.ToArray()).GetEnumerator(),
            r => ((CsvRecordEnumerator<byte>)r).Current
        );

        TestImpl(
            CsvReader.Enumerate("id,col,name\n1,null,John Doe", new() { Null = "null" }).GetEnumerator(),
            r => ((CsvRecordEnumerator<char>)r).Current
        );
        TestImpl(
            CsvReader.Enumerate("id,col,name\n1,null,John Doe"u8.ToArray(), new() { Null = "null" }).GetEnumerator(),
            r => ((CsvRecordEnumerator<byte>)r).Current
        );

        static void TestImpl(IDataReader reader, Func<IDataReader, IDataRecord> getRecord)
        {
            Assert.True(reader.Read());

            foreach (var obj in (IDataRecord[])[reader, getRecord(reader)])
            {
                Assert.Equal(3, obj.FieldCount);
                Assert.Equal("id", obj.GetName(0));
                Assert.Equal("col", obj.GetName(1));
                Assert.Equal("name", obj.GetName(2));

                Assert.False(obj.IsDBNull(0));
                Assert.True(obj.IsDBNull(1), $"Column 'col' should be DBNull, was: '{obj.GetValue(1)}'");
                Assert.False(obj.IsDBNull(2));
            }
        }
    }

    [Fact]
    public void Should_Parse_Complex_Values()
    {
        // data with datetimes and base64
        const string complexData =
            "id,created,updated,data\n"
            + "1,2023-10-01T12:00:00Z,2023-10-02T12:00:00Z,\"Hello, World!\"\n"
            + "2,2023-10-03T12:00:00Z,2023-10-04T12:00:00Z,\"Hello, World!\"\n";

        using CsvRecordEnumerator<char> charEnumerator = CsvReader.Enumerate(complexData).GetEnumerator();
        TestImpl(charEnumerator, r => ((CsvRecordEnumerator<char>)r).Current);

        using CsvRecordEnumerator<byte> byteEnumerator = CsvReader
            .Enumerate(Encoding.UTF8.GetBytes(complexData))
            .GetEnumerator();
        TestImpl(byteEnumerator, r => ((CsvRecordEnumerator<byte>)r).Current);

        static void TestImpl(IDataReader reader, Func<IDataReader, IDataRecord> getRecord)
        {
            Assert.True(reader.Read());

            Assert.Throws<NotSupportedException>(() => reader.GetSchemaTable());

            foreach (var obj in (IDataRecord[])[reader, getRecord(reader)])
            {
                Assert.Equal(4, obj.FieldCount);
                Assert.Equal("id", obj.GetName(0));
                Assert.Equal("created", obj.GetName(1));
                Assert.Equal("updated", obj.GetName(2));
                Assert.Equal("data", obj.GetName(3));

                Assert.Equal(1, obj.GetInt32(0));
                Assert.Equal(new DateTime(2023, 10, 1, 12, 0, 0, DateTimeKind.Utc).Date, obj.GetDateTime(1).Date);
                Assert.Equal(new DateTime(2023, 10, 2, 12, 0, 0, DateTimeKind.Utc).Date, obj.GetDateTime(2).Date);

                byte[] buffer = new byte[32];
                Assert.Equal("Hello, World!".Length, obj.GetBytes(3, 0, buffer, 0, 32));
                Assert.Equal("Hello, World!", Encoding.UTF8.GetString(buffer, 0, "Hello, World!".Length));

                // should read partial
                Assert.Equal("Hello".Length, obj.GetBytes(3, 0, buffer, 0, 5));
                Assert.Equal("Hello", Encoding.UTF8.GetString(buffer, 0, "Hello".Length));
            }

            Assert.True(reader.NextResult());
            Assert.False(reader.Read(), "Should not read more after two records");

            reader.Close();
            Assert.True(reader.IsClosed);
            Assert.Throws<ObjectDisposedException>(() => reader.Read());
        }
    }

    [Fact]
    public void Should_Return_Value()
    {
        using CsvRecordEnumerator<char> charEnumerator = CsvReader.Enumerate(data).GetEnumerator();
        TestImpl(charEnumerator, r => ((CsvRecordEnumerator<char>)r).Current);

        using CsvRecordEnumerator<byte> byteEnumerator = CsvReader
            .Enumerate(Encoding.UTF8.GetBytes(data))
            .GetEnumerator();
        TestImpl(byteEnumerator, r => ((CsvRecordEnumerator<byte>)r).Current);

        static void TestImpl(IDataReader reader, Func<IDataReader, IDataRecord> getRecord)
        {
            Assert.True(reader.Read());

            Assert.Equal(-1, reader.RecordsAffected);
            Assert.Equal(0, reader.Depth);
            Assert.False(reader.IsClosed);

            foreach (var obj in (IDataRecord[])[reader, getRecord(reader)])
            {
                Assert.Equal(8, obj.FieldCount);
                Assert.Equal("id", obj.GetName(0));
                Assert.Equal("name", obj.GetName(1));
                Assert.Equal("age", obj.GetName(2));
                Assert.Equal("height", obj.GetName(3));
                Assert.Equal("weight", obj.GetName(4));
                Assert.Equal("guid", obj.GetName(5));
                Assert.Equal("gender", obj.GetName(6));
                Assert.Equal("isactive", obj.GetName(7));

                Assert.Equal(0, obj.GetOrdinal("id"));
                Assert.Equal(1, obj.GetOrdinal("name"));
                Assert.Equal(2, obj.GetOrdinal("age"));
                Assert.Equal(3, obj.GetOrdinal("height"));
                Assert.Equal(4, obj.GetOrdinal("weight"));
                Assert.Equal(5, obj.GetOrdinal("guid"));
                Assert.Equal(6, obj.GetOrdinal("gender"));
                Assert.Equal(7, obj.GetOrdinal("isactive"));
                Assert.Equal(-1, obj.GetOrdinal("nonexistent"));

                Assert.Equal(1, obj.GetInt32(0));
                Assert.Equal("John Doe", obj.GetString(1));
                Assert.Equal(30, obj.GetInt32(2));
                Assert.Equal(180.5f, obj.GetFloat(3));
                Assert.Equal(75.0f, obj.GetFloat(4));
                Assert.Equal(new Guid("0123e456-7890-1234-5678-123456789012"), obj.GetGuid(5));
                Assert.Equal('M', obj.GetChar(6));
                Assert.True(obj.GetBoolean(7));

                Assert.Equal(1, obj.GetInt16(0));
                Assert.Equal(1, obj.GetInt64(0));
                Assert.Equal(1, obj.GetByte(0));
                Assert.Equal(180.5d, obj.GetDouble(3));
                Assert.Equal(180.5M, obj.GetDecimal(3));
                Assert.Equal(75.0d, obj.GetDouble(4));
                Assert.Equal(75.0M, obj.GetDecimal(4));

                object[] arr = new object[8];
                Assert.Equal(8, obj.GetValues(arr));
                Assert.Equal(
                    ["1", "John Doe", "30", "180.5", "75.0", "0123e456-7890-1234-5678-123456789012", "M", "true"],
                    arr
                );

                char[] buffer = new char[32];
                Assert.Equal("John Doe".Length, obj.GetChars(1, 0, buffer, 0, 32));
                Assert.Equal("John Doe", new string(buffer, 0, "John Doe".Length));

                // should read a partial string properly as well
                Assert.Equal("John".Length, obj.GetChars(1, 0, buffer, 0, 4));
                Assert.Equal("John", new string(buffer, 0, "John".Length));

                Assert.Throws<ArgumentException>(() => obj.GetValues(new object[5]));

                // should return strings for object overloads
                Assert.Equal("1", obj.GetValue(0));
                Assert.Equal("1", obj[0]);
                Assert.Equal("1", obj["id"]);

                Assert.Equal(typeof(string), obj.GetFieldType(0));
                Assert.Equal(typeof(string).FullName, obj.GetDataTypeName(0));

                Assert.Throws<ArgumentOutOfRangeException>(() => obj.GetBytes(5, fieldOffset: -1, new byte[32], 0, 16));
                Assert.Throws<ArgumentOutOfRangeException>(() => obj.GetBytes(5, fieldOffset: 255, new byte[32], 0, 16)
                );

                Assert.Throws<NotSupportedException>(() => obj.GetData(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => obj.GetDataTypeName(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => obj.GetDataTypeName(10));
                Assert.Throws<ArgumentOutOfRangeException>(() => obj.GetFieldType(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => obj.GetFieldType(10));
            }

            reader.Close();
            Assert.True(reader.IsClosed);
            Assert.Throws<ObjectDisposedException>(() => reader.Read());
        }
    }
}
