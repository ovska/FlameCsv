using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Writing.Escaping;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct UnixEscaper<T>(T quote, T escape) : IEscaper<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public T Quote
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => quote;
    }

    public T Escape
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => escape;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsEscaping(T value) => value == quote || value == escape;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastIndexOfEscapable(scoped ReadOnlySpan<T> value) => value.LastIndexOfAny(quote, escape);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountEscapable(scoped ReadOnlySpan<T> field)
    {
        ref T r0 = ref MemoryMarshal.GetReference(field);
        nint remaining = field.Length - 1;

        nint c0 = 0;
        nint c1 = 0;
        nint c2 = 0;
        nint c3 = 0;

        while (remaining >= 4)
        {
            c0 += Unsafe.Add(ref r0, remaining - 0) == quote || Unsafe.Add(ref r0, remaining - 0) == escape ? 1 : 0;
            c1 += Unsafe.Add(ref r0, remaining - 1) == quote || Unsafe.Add(ref r0, remaining - 1) == escape ? 1 : 0;
            c2 += Unsafe.Add(ref r0, remaining - 2) == quote || Unsafe.Add(ref r0, remaining - 2) == escape ? 1 : 0;
            c3 += Unsafe.Add(ref r0, remaining - 3) == quote || Unsafe.Add(ref r0, remaining - 3) == escape ? 1 : 0;
            remaining -= 4;
        }

        while (remaining >= 0)
        {
            c0 += Unsafe.Add(ref r0, remaining) == quote || Unsafe.Add(ref r0, remaining) == escape ? 1 : 0;
            remaining--;
        }

        return (int)(c0 + c1 + c2 + c3);
    }
}
