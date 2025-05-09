using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Converters;

internal sealed class BooleanUtf8Converter : CsvConverter<byte, bool>
{
    public static readonly BooleanUtf8Converter Instance = new();

    public override bool TryFormat(Span<byte> destination, bool value, out int charsWritten)
    {
        // source: dotnet runtime bool.TryFormat
        if (value)
        {
            if (destination.Length > 3)
            {
                uint trueVal = BitConverter.IsLittleEndian ? 0x65757274u : 0x74727565; // "True"
                MemoryMarshal.Write(destination, in trueVal);
                charsWritten = 4;
                return true;
            }
        }
        else
        {
            if (destination.Length > 4)
            {
                uint falsVal = BitConverter.IsLittleEndian ? 0x736C6166u : 0x66616C73; // "Fals"
                MemoryMarshal.Write(destination, in falsVal);
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
