using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Writing;

[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct UnixEscaper<T>(T quote, T escape) : IEscaper<T> where T : unmanaged, IEquatable<T>
{
    public T Quote => quote;
    public T Escape => escape;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsEscaping(T value) => value.Equals(quote) || value.Equals(escape);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastIndexOfEscapable(scoped ReadOnlySpan<T> value) => value.LastIndexOfAny(quote, escape);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CountEscapable(scoped ReadOnlySpan<T> field)
    {
        ref T r0 = ref MemoryMarshal.GetReference(field);
        nint rem = field.Length - 1;

        nint c0 = 0;
        nint c1 = 0;
        nint c2 = 0;
        nint c3 = 0;

        while (rem >= 4)
        {
            c0 += (Unsafe.Add(ref r0, rem - 0).Equals(quote) || Unsafe.Add(ref r0, rem - 0).Equals(escape)) ? 1 : 0;
            c1 += (Unsafe.Add(ref r0, rem - 1).Equals(quote) || Unsafe.Add(ref r0, rem - 1).Equals(escape)) ? 1 : 0;
            c2 += (Unsafe.Add(ref r0, rem - 2).Equals(quote) || Unsafe.Add(ref r0, rem - 2).Equals(escape)) ? 1 : 0;
            c3 += (Unsafe.Add(ref r0, rem - 3).Equals(quote) || Unsafe.Add(ref r0, rem - 3).Equals(escape)) ? 1 : 0;
            rem -= 4;
        }

        while (rem >= 0)
        {
            c0 += (Unsafe.Add(ref r0, rem).Equals(quote) || Unsafe.Add(ref r0, rem).Equals(escape)) ? 1 : 0;
            rem--;
        }

        return (int)(c0 + c1 + c2 + c3);
    }
}
