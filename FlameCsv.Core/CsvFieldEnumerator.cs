using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public struct CsvFieldEnumerator<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public ReadOnlyMemory<T> Current { get; private set; }

    private readonly ArrayPool<T>? _arrayPool;
    private T[]? _toReturn;

    private CsvEnumerationStateRef<T> _state;

    internal CsvFieldEnumerator(ReadOnlyMemory<T> value, in CsvReadingContext<T> context)
    {
        Throw.IfDefaultStruct<CsvFieldEnumerator<T>>(context.ArrayPool);

        _arrayPool = context.ArrayPool;
        _state = new CsvEnumerationStateRef<T>(in context, value, ref _toReturn);
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

    public void Reset() => throw new NotSupportedException();

    public void Dispose()
    {
        _arrayPool?.EnsureReturned(ref _toReturn);
    }
}
