using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NewlineBuffer<T> where T : unmanaged, IBinaryInteger<T>
{
    public static readonly NewlineBuffer<T> CRLF = new(2, T.CreateChecked('\r'), T.CreateChecked('\n'));
    public static readonly NewlineBuffer<T> LF = new(1, T.CreateChecked('\n'), T.CreateChecked('\n'));

    public readonly int Length;
    public readonly T First;
    public readonly T Second;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NewlineBuffer(scoped ReadOnlySpan<T> values)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Length, 2);
        First = values[0];
        Second = values.Length == 1 ? values[0] : values[1];
        Length = values.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NewlineBuffer(int length, T first, T second)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)length, 2u);
        First = first;
        Second = second;
        Length = length;
    }
}
