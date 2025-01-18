using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv;

internal readonly struct NewlineBuffer<T> where T : unmanaged, IBinaryInteger<T>
{
    public static NewlineBuffer<T> CRLF
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(2, T.CreateChecked('\r'), T.CreateChecked('\n'));
    }

    public static NewlineBuffer<T> LF
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(1, T.CreateChecked('\n'), T.CreateChecked('\n'));
    }

    public readonly int Length;
    public readonly T First;
    public readonly T Second;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NewlineBuffer(
        scoped ReadOnlySpan<T> values,
        [CallerArgumentExpression(nameof(values))]
        string paramName = "")
    {
        if (values.Length == 2)
        {
            Length = 2;
            First = values[0];
            Second = values[1];
        }
        else if (values.Length == 1)
        {
            Length = 1;
            First = values[0];
            Second = values[0];
        }
        else
        {
            Throw.Argument_OutOfRange(paramName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NewlineBuffer(int length, T first, T second)
    {
        Debug.Assert(length == 2 || (length == 1 && first == second));
        First = first;
        Second = second;
        Length = length;
    }

    [Conditional("DEBUG")]
    internal void AssertInitialized() => Debug.Assert(Length != 0);
}
