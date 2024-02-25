using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public struct CsvFieldEnumerator<T> : IDisposable, IEnumerator<ReadOnlyMemory<T>> where T : unmanaged, IEquatable<T>
{
    public ReadOnlyMemory<T> Current { get; private set; }

    readonly object IEnumerator.Current => Current;

    private readonly ArrayPool<T>? _arrayPool;
    private T[]? _toReturn;

    private CsvFieldReader<T> _state;

    internal CsvFieldEnumerator(ReadOnlyMemory<T> value, in CsvReadingContext<T> context)
    {
        Throw.IfDefaultStruct<CsvFieldEnumerator<T>>(context.ArrayPool);

        _arrayPool = context.ArrayPool;
        _state = new CsvFieldReader<T>(in context, value, ref _toReturn);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_state.TryReadNext(out ReadOnlyMemory<T> field))
        {
            Current = field;
            return true;
        }

        Current = default;
        return false;
    }

    public readonly void Reset() => throw new NotSupportedException();

    public void Dispose()
    {
        _arrayPool?.EnsureReturned(ref _toReturn);
    }
}
