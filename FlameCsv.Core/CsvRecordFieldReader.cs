using System.Diagnostics.CodeAnalysis;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

internal struct CsvRecordFieldReader<T> : ICsvFieldReader<T> where T : unmanaged, IEquatable<T>
{
    private readonly ArraySegment<ReadOnlyMemory<T>> _values;
    private readonly CsvReadingContext<T> _context;
    private int _index;

    public CsvRecordFieldReader(ArraySegment<ReadOnlyMemory<T>> values, in CsvReadingContext<T> context)
    {
        _values = values;
        _context = context;
    }

    public readonly void EnsureFullyConsumed(int fieldCount)
    {
        if (_index != _values.Count)
            Throw.InvalidData_FieldCount(fieldCount, _values.Count);
    }

    [DoesNotReturn]
    public readonly void ThrowForInvalidEOF()
    {
        Throw.InvalidData_FieldCount();
    }

    [DoesNotReturn]
    public readonly void ThrowParseFailed(ReadOnlyMemory<T> field, CsvConverter<T>? parser)
    {
        string withStr = parser is null ? "" : $" with {parser.GetType()}";

        throw new CsvParseException(
            $"Failed to parse{withStr} from {_context.AsPrintableString(field)}.")
        { Parser = parser };
    }

    public readonly void TryEnsureFieldCount(int fieldCount)
    {
        if (_values.Count != fieldCount)
            Throw.InvalidData_FieldCount(fieldCount, _values.Count);
    }

    public bool TryReadNext(out ReadOnlyMemory<T> field)
    {
        var values = _values.AsSpan();

        if ((uint)_index < (uint)values.Length)
        {
            field = values[_index++];
            return true;
        }

        _index = -1;
        field = default;
        return false;
    }
}

