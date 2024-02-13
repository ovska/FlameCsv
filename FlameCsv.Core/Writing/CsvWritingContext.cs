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

    public CsvDialect<T> Dialect => _dialect;
    public CsvOptions<T> Options { get; }
    public ArrayPool<T> ArrayPool { get; }
    public bool HasHeader { get; }
    public CsvFieldQuoting FieldQuoting { get; }

    private readonly CsvDialect<T> _dialect;

    public CsvWritingContext(CsvOptions<T> options, in CsvContextOverride<T> overrides)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        Options = options;
        ArrayPool = overrides._arrayPool.Resolve(options.ArrayPool).AllocatingIfNull();
        FieldQuoting = overrides._fieldQuoting.Resolve(options.FieldQuoting);
        HasHeader = overrides._hasHeader.Resolve(options.HasHeader);
        _dialect = new CsvDialect<T>(options, in overrides);
    }
}

