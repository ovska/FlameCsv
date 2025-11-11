using System.Buffers;
using System.Diagnostics;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Provides static methods for reading CSV data.
/// </summary>
[PublicAPI]
public static partial class CsvReader
{
    private static FileStream GetFileStream(string path, bool isAsync, in CsvIOOptions ioOptions)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ioOptions.BufferSize,
            FileOptions.SequentialScan | (isAsync ? FileOptions.Asynchronous : FileOptions.None)
        );
    }

    private static StreamBufferReader GetFileBufferReader(
        string path,
        bool isAsync,
        MemoryPool<byte> memoryPool,
        CsvIOOptions ioOptions
    )
    {
        return new StreamBufferReader(GetFileStream(path, isAsync, in ioOptions), memoryPool, in ioOptions);
    }

    private static ICsvBufferReader<char> GetFileBufferReader(
        string path,
        Encoding? encoding,
        bool isAsync,
        MemoryPool<char> memoryPool,
        CsvIOOptions ioOptions
    )
    {
        FileStream stream = GetFileStream(path, isAsync, in ioOptions);

        try
        {
            if (encoding is null || encoding.Equals(Encoding.UTF8))
            {
                return new Utf8StreamReader(stream, memoryPool, in ioOptions);
            }

            return CsvBufferReader.Create(
                new StreamReader(
                    stream,
                    encoding,
                    detectEncodingFromByteOrderMarks: true,
                    ioOptions.BufferSize,
                    leaveOpen: false
                ),
                memoryPool,
                ioOptions
            );
        }
        catch
        {
            // exception before we returned control to the caller
            stream.Dispose();
            throw;
        }
    }
}

internal readonly struct ReaderFactory<T>
    where T : unmanaged
{
    private readonly ICsvBufferReader<T>? _value;
    private readonly Func<bool, ICsvBufferReader<T>>? _factory;

    public ReaderFactory(ICsvBufferReader<T> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _value = value;
    }

    public ReaderFactory(Func<bool, ICsvBufferReader<T>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public ICsvBufferReader<T> Create(bool isAsync)
    {
        if (_value is not null)
        {
            return _value;
        }

        Debug.Assert(_factory is not null, "Uninitialized ReaderFactory");
        return _factory(isAsync);
    }
}
