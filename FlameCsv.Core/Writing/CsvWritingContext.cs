using System.Buffers;
using FlameCsv.Extensions;

namespace FlameCsv.Writing;

internal readonly struct CsvWritingContext<T> where T : unmanaged, IEquatable<T>
{
    public void EnsureValid()
    {
        if (Options is null)
            Throw.InvalidOp_DefaultStruct(typeof(CsvWritingContext<T>));
    }

    public readonly CsvDialect<T> Dialect;
    public readonly CsvOptions<T> Options;
    public ArrayPool<T> ArrayPool => Options._arrayPool.AllocatingIfNull();
    public bool HasHeader => Options._hasHeader;
    public CsvFieldQuoting FieldQuoting => Options._fieldQuoting;

    public CsvWritingContext(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        Options = options;
        Dialect = new CsvDialect<T>(options);
    }
}

