using System.Buffers.Binary;
using System.Runtime.CompilerServices;

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

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<byte> source, out bool value)
    {
        // source: dotnet runtime Utf8Parser

        if (source.Length == 4)
        {
            int dw = BinaryPrimitives.ReadInt32LittleEndian(source) & ~0x20202020;
            if (
                dw == 0x45555254 /* 'EURT' */
            )
            {
                value = true;
                return true;
            }
        }

        if (source.Length == 5)
        {
            int dw = BinaryPrimitives.ReadInt32LittleEndian(source) & ~0x20202020;
            if (
                dw == 0x534c4146 /* 'SLAF' */
                && (source[4] & ~0x20) == 'E'
            )
            {
                value = false;
                return true;
            }
        }

        Unsafe.SkipInit(out value);
        return false;
    }
}
