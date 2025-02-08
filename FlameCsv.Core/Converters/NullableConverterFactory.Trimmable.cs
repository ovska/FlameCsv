using System.Runtime.InteropServices;
#if DEBUG
using Unsafe = FlameCsv.Extensions.DebugUnsafe
#else
using Unsafe = System.Runtime.CompilerServices.Unsafe
#endif
    ;

namespace FlameCsv.Converters;

internal static class TrimmableNullableConverter
{
    public static CsvConverter<T, TValue?> Create<T, TValue>(
        CsvConverter<T, TValue> inner,
        ReadOnlyMemory<T> nullToken)
        where T : unmanaged, IBinaryInteger<T>
        where TValue : struct
    {
        if (nullToken.IsEmpty)
        {
            return new OptimizedNullEmptyConverter<T, TValue>(inner);
        }

        if (nullToken.Length <= Container<T>.MaxLength)
        {
            return new OptimizedKnownLengthConverter<T, TValue>(inner, nullToken);
        }

        if (MemoryMarshal.TryGetArray(nullToken, out var array))
        {
            return new OptimizedNullArrayConverter<T, TValue>(inner, array);
        }

        if (typeof(T) == typeof(char))
        {
            var nullTokenChar = Unsafe.As<ReadOnlyMemory<T>, ReadOnlyMemory<char>>(ref nullToken);
            if (MemoryMarshal.TryGetString(nullTokenChar, out var value, out var start, out var length))
            {
                return (CsvConverter<T, TValue?>)(object)new OptimizedNullStringConverter<TValue>(
                    Unsafe.As<CsvConverter<char, TValue>>(inner),
                    value,
                    start,
                    length);
            }
        }

        return new NullableConverter<T, TValue>(inner, nullToken);
    }
}
