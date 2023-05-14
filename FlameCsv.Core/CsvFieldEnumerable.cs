using FlameCsv.Extensions;

namespace FlameCsv;

public readonly struct CsvFieldEnumerable<T> where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlyMemory<T> _record;
    private readonly CsvReadingContext<T>? _context;

    public CsvFieldEnumerable(
        ReadOnlyMemory<T> value,
        CsvOptions<T> options,
        CsvContextOverride<T> overrides = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        _record = value;
        _context = new CsvReadingContext<T>(options, overrides);
    }

    public CsvFieldEnumerator<T> GetEnumerator()
    {
        Throw.IfDefaultStruct<CsvFieldEnumerable<T>>(_context);
        return new CsvFieldEnumerator<T>(_record, _context.Value);
    }

    /// <summary>
    /// Enumerates the data and allocates copies of the fields and returns them in a list <see cref="List{T}"/>.
    /// </summary>
    public List<ReadOnlyMemory<T>> ToList()
    {
        List<ReadOnlyMemory<T>> list = new();

        foreach (ReadOnlyMemory<T> field in this)
        {
            list.Add(field.SafeCopy());
        }

        return list;
    }
}
