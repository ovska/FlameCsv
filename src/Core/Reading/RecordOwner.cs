using System.Runtime.CompilerServices;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

/// <summary>
/// Base class for types that provide ownership of CSV records.
/// </summary>
public abstract class RecordOwner<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// CSV options associated with this record owner.
    /// </summary>
    public CsvOptions<T> Options { get; }

    private protected RecordOwner(CsvOptions<T> options, RecordBuffer recordBuffer)
    {
        options.MakeReadOnly();
        Options = options;
        _recordBuffer = recordBuffer;

        _quoteAndLeniency = (byte)(
            options.Quote.GetValueOrDefault()
            | (options.ValidateQuotes == CsvQuoteValidation.AllowInvalid ? 0x80 : 0x00)
        );

        // remove undefined bits so we can cmp against zero later
        Trimming = options.Trimming & CsvFieldTrimming.Both;
    }

    internal readonly RecordBuffer _recordBuffer;

    internal readonly CsvFieldTrimming Trimming;
    private readonly byte _quoteAndLeniency; // use byte field to ensure the struct is only 2 bytes

    internal T Quote
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get =>
            Unsafe.SizeOf<T>() switch
            {
                1 => Unsafe.BitCast<byte, T>((byte)(_quoteAndLeniency & 0x7F)),
                2 => Unsafe.BitCast<char, T>((char)(_quoteAndLeniency & 0x7F)),
                _ => throw Token<T>.NotSupported,
            };
    }

    internal bool AcceptInvalidQuotes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (sbyte)_quoteAndLeniency < 0;
    }

    /// <summary>
    /// Indicates whether the reader has been disposed.
    /// </summary>
    public abstract bool IsDisposed { get; }

    /// <summary>
    /// Returns a buffer that can be used for unescaping field data.
    /// The buffer is not valid after disposing the reader.
    /// </summary>
    internal abstract Span<T> GetUnescapeBuffer(int length);
}
