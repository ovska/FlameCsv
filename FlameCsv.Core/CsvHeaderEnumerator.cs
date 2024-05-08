using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using FlameCsv.Reading;

namespace FlameCsv;

public ref struct CsvHeaderEnumerator<T> where T : unmanaged, IEquatable<T>
{
    private readonly Span<char> _buffer;

    private CsvFieldReader<T> _enumerator;

    private readonly string[]? _array;
    private int _index;

    internal CsvHeaderEnumerator(
        ref CsvFieldReader<T> enumerator,
        Span<char> buffer = default)
    {
        Guard.IsTrue(enumerator.isAtStart);
        _enumerator = enumerator;
        _buffer = buffer;
    }

    public CsvHeaderEnumerator(string[] headers)
    {
        _array = headers;
    }

    public bool TryReadNext(out ReadOnlySpan<char> header)
    {
        if (HasEnumerator)
        {
            if (!_enumerator.TryReadNext(out ReadOnlySpan<T> field))
            {
                if (_enumerator.Context.Options.TryGetChars(field, _buffer, out int charsWritten))
                {
                    header = _buffer[..charsWritten];
                }
                else
                {
                    header = _enumerator.Context.Options.GetAsString(field);
                }

                return true;
            }
        }
        else if (_index < _array.Length)
        {
            header = _array[_index++];
            return true;
        }

        header = default;
        return false;
    }

    [MemberNotNullWhen(false, nameof(_array))]
    private readonly bool HasEnumerator => _array is null;
}
