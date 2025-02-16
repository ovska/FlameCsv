using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <inheritdoc cref="CsvRecordEnumeratorBase{T}"/>
[PublicAPI]
public sealed class CsvRecordEnumerator<T> : CsvRecordEnumeratorBase<T>, IEnumerator<CsvValueRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    internal CsvRecordEnumerator(
        ReadOnlyMemory<T> data,
        CsvOptions<T> options)
        : base(options)
    {
        _parser.SetData(new ReadOnlySequence<T>(data));
    }

    internal CsvRecordEnumerator(
        in ReadOnlySequence<T> data,
        CsvOptions<T> options)
        : base(options)
    {
        _parser.SetData(in data);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (MoveNextCore(isFinalBlock: false) || MoveNextCore(isFinalBlock: true))
        {
            return true;
        }

        // reached the end of data
        _current = default;
        return false;
    }

    void IEnumerator.Reset() => throw new NotSupportedException();
    object IEnumerator.Current => Current;
    CsvValueRecord<T> IEnumerator<CsvValueRecord<T>>.Current => Current;

    /// <summary>
    /// Gets the current record.
    /// </summary>
    /// <remarks>
    /// The value should not be held onto after the enumeration continues or ends, as the records might wrap
    /// shared or pooled memory.
    /// If you must, convert the record to <see cref="CsvRecord{T}"/>.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the enumerator has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when enumeration has not yet started.</exception>
    public ref readonly CsvValueRecord<T> Current
    {
        get
        {
            if (_current._options is null) ThrowInvalidCurrentAccess();
            return ref _current;
        }
    }
}
