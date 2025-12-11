using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

internal readonly ref struct FieldBuffer
{
    public Span<uint> Fields { get; init; }
    public Span<byte> Quotes { get; init; }

    private readonly ref byte _degenerateQuotes;

    public bool DegenerateQuotes
    {
        get => Unsafe.BitCast<byte, bool>(_degenerateQuotes);
        set => _degenerateQuotes = Unsafe.BitCast<bool, byte>(value);
    }

    public FieldBuffer()
    {
        _degenerateQuotes = ref Unsafe.NullRef<byte>();
    }

    public FieldBuffer(int start, RecordBuffer recordBuffer)
    {
        Fields = recordBuffer._fields.AsSpan(start);
        Quotes = recordBuffer._quotes.AsSpan(start);
        _degenerateQuotes = ref recordBuffer._quotes[0];
    }
}
