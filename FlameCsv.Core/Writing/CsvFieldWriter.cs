using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.Writing;

internal readonly struct CsvFieldWriter<T, TWriter> : ICsvFieldWriter<T>
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IAsyncBufferWriter<T>
{
    private readonly CsvRecordWriter<T, TWriter> _impl;

    public CsvFieldWriter(CsvRecordWriter<T, TWriter> impl)
    {
        _impl = impl;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask TryFlushAsync(CancellationToken cancellationToken = default)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            return !_impl.NeedsFlush
                ? default
                : _impl.FlushAsync(cancellationToken);
        }

        return ValueTask.FromCanceled(cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDelimiter()
    {
        _impl.WriteDelimiter();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNewline()
    {
        _impl.WriteNewline();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteField<TValue>(CsvConverter<T, TValue> converter, [AllowNull] TValue value)
    {
        _impl.WriteValue(converter!, value);
    }
}
