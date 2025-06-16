using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Converters;

internal sealed class BooleanUtf8Converter : CsvConverter<byte, bool>
{
    public static readonly BooleanUtf8Converter Instance = new();

    public override bool TryFormat(Span<byte> destination, bool value, out int charsWritten)
    {
        if (value)
        {
            if (destination.Length > 3)
            {
                // JIT unrolls these to a single uint32 write
                destination[0] = (byte)'t';
                destination[1] = (byte)'r';
                destination[2] = (byte)'u';
                destination[3] = (byte)'e';
                charsWritten = 4;
                return true;
            }
        }
        else
        {
            if (destination.Length > 4)
            {
                // JIT unrolls these to a single uint32 write and one byte write
                destination[0] = (byte)'f';
                destination[1] = (byte)'a';
                destination[2] = (byte)'l';
                destination[3] = (byte)'s';
                destination[4] = (byte)'e';
                charsWritten = 5;
                return true;
            }
        }

        charsWritten = 0;
        return false;
    }

    public override bool TryParse(ReadOnlySpan<byte> source, out bool value)
    {
        if (source.Length == 4)
        {
            ref byte first = ref MemoryMarshal.GetReference(source);

            if ((Unsafe.ReadUnaligned<uint>(in first) | 0x20202020) == MemoryMarshal.Read<uint>("true"u8))
            {
                value = true;
                return true;
            }
        }

        if (source.Length == 5)
        {
            ref byte first = ref MemoryMarshal.GetReference(source);

            if (
                (Unsafe.ReadUnaligned<uint>(in first) | 0x20202020) == MemoryMarshal.Read<uint>("fals"u8)
                && (Unsafe.Add(ref first, 4) | 0x20) == (byte)'e'
            )
            {
                value = false;
                return true;
            }
        }

        value = false;
        return false;
    }
}
