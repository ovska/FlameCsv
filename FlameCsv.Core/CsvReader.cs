using System.Buffers;
using System.Text;
using FlameCsv.IO;
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
            if (encoding is null || encoding == Encoding.UTF8 || encoding == Encoding.ASCII)
            {
                return new Utf8StreamReader(stream, memoryPool, in ioOptions);
            }

            return CsvBufferReader.Create(
                new StreamReader(
                    stream,
                    encoding,
                    detectEncodingFromByteOrderMarks: encoding is null,
                    ioOptions.BufferSize,
                    leaveOpen: false
                ),
                memoryPool,
                ioOptions
            );
        }
        catch
        {
            try
            {
                if (isAsync)
                {
                    stream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                else
                {
                    stream.Dispose();
                }
            }
            catch { }

            throw;
        }
    }
}
