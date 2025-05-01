using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail. This type should probably not be used directly.
/// Using an unitialized instance leads to undefined behavior.
/// </summary>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
[PublicAPI]
public readonly ref struct CsvFieldsRef<T> : ICsvFields<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvReader<T> _reader;
    private readonly ref T _data;
    private readonly Span<T> _unescapeBuffer;
    private readonly ReadOnlySpan<Field> _fields;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(scoped ref readonly CsvFields<T> fields, Span<T> unescapeBuffer)
    {
        CsvReader<T> reader = fields.Reader;

        _reader = reader;
        _data = ref MemoryMarshal.GetReference(fields.Data.Span);
        _fields = fields.Fields;
        _unescapeBuffer = unescapeBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(CsvReader<T> reader, ref T data, ReadOnlySpan<Field> fields, Span<T> unescapeBuffer)
    {
        _reader = reader;
        _data = ref data;
        _fields = fields;
        _unescapeBuffer = unescapeBuffer;
    }

    /// <inheritdoc/>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fields.Length;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Field field = _fields[index];

            ReadOnlySpan<T> span = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref _data, field.Start),
                field.Length
            );

            if (field.NeedsProcessing)
            {
                // TODO
            }

            return span;
        }
    }

    /// <summary>
    /// Returns the raw unescaped span of the field at the specified index.
    /// </summary>
    /// <param name="index">0-based field index</param>
    /// <exception cref="IndexOutOfRangeException">
    /// Thrown if <paramref name="index"/> is less than 0 or greater than or equal to <see cref="FieldCount"/>
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRawSpan(int index)
    {
        Field field = _fields[index];
        field.GetRawSpan(out int start, out int length);
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), length);
    }

    /// <inheritdoc cref="CsvFields{T}.Record"/>
    public ReadOnlySpan<T> Record
    {
        get
        {
            int start = _fields[0].Start;
            (_, int end) = _fields[^1];

            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), end - start);
        }
    }

    /// <summary>
    /// Returns a diagnostic string representation of the current instance.
    /// </summary>
    /// <remarks>
    /// See <see cref="Record"/> to get the actual record span.
    /// </remarks>
    public override string ToString()
    {
        if (FieldCount == 0)
        {
            return $"{{ CsvFieldsRef<{Token<T>.Name}>[{FieldCount}]: Uninitialized }}";
        }

        if (typeof(T) == typeof(char))
        {
            return $"{{ CsvFieldsRef<{Token<T>.Name}>[{FieldCount}]: \"{MemoryMarshal.Cast<T, char>(Record)}\" }}";
        }

        if (typeof(T) == typeof(byte))
        {
            return $"{{ CsvFieldsRef<{Token<T>.Name}>[{FieldCount}]: \"{Encoding.UTF8.GetString(MemoryMarshal.AsBytes(Record))}\" }}";
        }

        return $"{{ CsvFieldsRef<{Token<T>.Name}>[{FieldCount}]: Length: {Record.Length} }}";
    }
}
