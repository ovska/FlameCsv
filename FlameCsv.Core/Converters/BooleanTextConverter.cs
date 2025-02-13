using System.Runtime.InteropServices;

namespace FlameCsv.Converters;

internal sealed class BooleanTextConverter : CsvConverter<char, bool>
{
    public static BooleanTextConverter Instance { get; } = new();

    public override bool TryFormat(Span<char> destination, bool value, out int charsWritten)
    {
        // source: dotnet runtime bool.TryFormat
        if (value)
        {
            if (destination.Length > 3)
            {
                ulong trueVal = BitConverter.IsLittleEndian ? 0x65007500720074ul : 0x74007200750065ul; // "true"
                MemoryMarshal.Write(MemoryMarshal.AsBytes(destination), in trueVal);
                charsWritten = 4;
                return true;
            }
        }
        else
        {
            if (destination.Length > 4)
            {
                ulong falsVal = BitConverter.IsLittleEndian ? 0x73006C00610066ul : 0x660061006C0073ul; // "fals"
                MemoryMarshal.Write(MemoryMarshal.AsBytes(destination), in falsVal);
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
