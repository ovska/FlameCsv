using System.Data;
using FlameCsv.IO;
using FlameCsv.Reading;
using JetBrains.Annotations;
using DAMT = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace FlameCsv;

public class CsvDataReader<T> : IDataReader, IAsyncDisposable where T : unmanaged, IBinaryInteger<T>
{
    public int FieldCount => _current.FieldCount;

    [HandlesResourceDisposal]
    private readonly CsvParser<T> _parser;

    private bool _needHeader;
    private CsvHeader? _header;
    private CsvFields<T> _current;

    public CsvDataReader(CsvOptions<T> options, ICsvPipeReader<T> reader)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);

        _parser = CsvParser.Create(options, reader);
        _needHeader = options.HasHeader;
    }

    /// <inheritdoc />
    public bool GetBoolean(int i)
    {
        throw new NotImplementedException();
    }

    public byte GetByte(int i)
    {
        throw new NotImplementedException();
    }

    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
    {
        throw new NotImplementedException();
    }

    public char GetChar(int i)
    {
        throw new NotImplementedException();
    }

    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
    {
        throw new NotImplementedException();
    }

    public IDataReader GetData(int i)
    {
        throw new NotImplementedException();
    }

    public string GetDataTypeName(int i)
    {
        throw new NotImplementedException();
    }

    public DateTime GetDateTime(int i)
    {
        throw new NotImplementedException();
    }

    public decimal GetDecimal(int i)
    {
        throw new NotImplementedException();
    }

    public double GetDouble(int i)
    {
        throw new NotImplementedException();
    }

    [return: DAM(DAMT.PublicFields | DAMT.PublicProperties)]
    public Type GetFieldType(int i)
    {
        throw new NotImplementedException();
    }

    public float GetFloat(int i)
    {
        throw new NotImplementedException();
    }

    public Guid GetGuid(int i)
    {
        throw new NotImplementedException();
    }

    public short GetInt16(int i)
    {
        throw new NotImplementedException();
    }

    public int GetInt32(int i)
    {
        throw new NotImplementedException();
    }

    public long GetInt64(int i)
    {
        throw new NotImplementedException();
    }

    public string GetName(int i)
    {
        throw new NotImplementedException();
    }

    public int GetOrdinal(string name)
    {
        throw new NotImplementedException();
    }

    public string GetString(int i)
    {
        throw new NotImplementedException();
    }

    public object GetValue(int i)
    {
        throw new NotImplementedException();
    }

    public int GetValues(object[] values)
    {
        throw new NotImplementedException();
    }

    public bool IsDBNull(int i)
    {
        throw new NotImplementedException();
    }

    public object this[int i] => throw new NotImplementedException();

    public object this[string name] => throw new NotImplementedException();

    public void Close() => Dispose();

    public DataTable? GetSchemaTable()
    {
        throw new NotImplementedException();
    }

    public bool NextResult() => Read();

    public bool Read()
    {
        ObjectDisposedException.ThrowIf(IsClosed, this);

        Retry:
        if (!_parser.TryReadLine(out _current, false))
        {

        }

        return true;
    }

    public int Depth => 0;
    public bool IsClosed { get; private set; }
    public int RecordsAffected => -1;

    public void Dispose()
    {
        if (IsClosed)
            return;

        IsClosed = true;
        GC.SuppressFinalize(this);
        _parser.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (IsClosed)
            return;

        IsClosed = true;
        GC.SuppressFinalize(this);
        await _parser.DisposeAsync().ConfigureAwait(false);
    }
}
