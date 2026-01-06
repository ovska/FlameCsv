using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Converters;

internal sealed class BooleanTextConverter : CsvConverter<char, bool>
{
    public static BooleanTextConverter Instance { get; } = new();

    public override bool TryFormat(Span<char> destination, bool value, out int charsWritten)
    {
        // JIT doesn't unroll char writes like it does for byte as of .NET 10, and TryCopyTo produces slightly worse codegen than this
        if (value)
        {
            if (destination.Length >= 4)
            {
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
