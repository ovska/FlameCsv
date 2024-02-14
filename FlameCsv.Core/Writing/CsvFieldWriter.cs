using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FlameCsv.Writing;

internal readonly struct CsvFieldWriter<T, TWriter>(CsvRecordWriter<T, TWriter> impl)
    : ICsvFieldWriter<T>
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IAsyncBufferWriter<T>
{
    private readonly CsvRecordWriter<T, TWriter> _impl = impl;

    public bool NeedsFlush => _impl.NeedsFlush;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
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
        _impl.WriteField(converter!, value);
    }

    public void WriteText(ReadOnlySpan<char> text) => _impl.WriteText(text);

    public void WriteRaw(ReadOnlySpan<T> span) => _impl.WriteRaw(span);

    public ValueTask DisposeAsync() => _impl.DisposeAsync();
}
