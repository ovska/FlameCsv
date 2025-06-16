using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Converters;

internal sealed class BooleanTextConverter : CsvConverter<char, bool>
{
    public static BooleanTextConverter Instance { get; } = new();

    public override bool TryFormat(Span<char> destination, bool value, out int charsWritten)
    {
        if (value)
        {
            if (destination.Length >= 4)
            {
                // JIT doesn't yet unroll successive char writes, so do it manually
                Unsafe.WriteUnaligned(
                    ref Unsafe.As<char, byte>(ref destination[0]),
                    MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes<char>("true"))
                );
                charsWritten = 4;
                return true;
            }
        }
        else
        {
            if (destination.Length >= 5)
            {
                // JIT doesn't unroll successive char writes, so do it manually
                Unsafe.WriteUnaligned(
                    ref Unsafe.As<char, byte>(ref destination[0]),
                    MemoryMarshal.Read<ulong>(MemoryMarshal.AsBytes<char>("fals"))
                );
                destination[4] = 'e';
                charsWritten = 5;
                return true;
            }
        }

        charsWritten = 0;
        return false;
    }

    public override bool TryParse(ReadOnlySpan<char> source, out bool value)
    {
        return bool.TryParse(source, out value);
    }
}
