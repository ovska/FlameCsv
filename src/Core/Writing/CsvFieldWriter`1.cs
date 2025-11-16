using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.IO.Internal;
using FlameCsv.Writing.Escaping;
using JetBrains.Annotations;

namespace FlameCsv.Writing;

/// <summary>
/// Writes CSV fields and handles escaping as needed.
/// </summary>
/// <remarks>
/// This type must be disposed to release rented memory.
/// </remarks>
[MustDisposeResource]
public readonly struct CsvFieldWriter<T> : IDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// The <see cref="ICsvBufferWriter{T}"/> this instance writes to.
    /// </summary>
    public ICsvBufferWriter<T> Writer { get; }

    /// <summary>
    /// The options-instance for this writer.
    /// </summary>
    public CsvOptions<T> Options { get; }

    private readonly T _delimiter;
    private readonly T _quote;
    private readonly bool _isCRLF;
    private readonly IQuoter<T> _quoter;
    private readonly Allocator<T> _allocator;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public CsvFieldWriter(ICsvBufferWriter<T> writer, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);

        options.MakeReadOnly();

        Writer = writer;
        Options = options;

        _delimiter = T.CreateTruncating(options.Delimiter);
        _quote = T.CreateTruncating(options.Quote);
        _quoter = Quoter.Create(options);
        _isCRLF = options.Newline.IsCRLF();
        _allocator = new MemoryPoolAllocator<T>(options.Allocator);
    }

    /// <summary>
    /// Writes <paramref name="value"/> to the writer using <paramref name="converter"/>.
    /// </summary>
    public void WriteField<TValue>(CsvConverter<T, TValue> converter, TValue? value)
    {
        // string is the most common reference type, and most likely not using a custom converter
        if (!typeof(TValue).IsValueType && ReferenceEquals(converter, Converters.StringTextConverter.Instance))
        {
            Debug.Assert(typeof(TValue) == typeof(string));
            WriteText(Unsafe.As<string?>(value));
            return;
        }

        int tokensWritten;
        scoped Span<T> destination;

        if (value is not null || converter.CanFormatNull)
        {
            destination = Writer.GetSpan();

            while (!converter.TryFormat(destination, value!, out tokensWritten))
            {
                destination = Writer.GetSpan(destination.Length * 2);
            }
        }
        else
        {
            ReadOnlySpan<T> nullValue = Options.GetNullSpan(typeof(TValue));
            destination = Writer.GetSpan(nullValue.Length);
            nullValue.CopyTo(destination);
            tokensWritten = nullValue.Length;
        }

        // validate negative or too large tokensWritten in case of broken user-defined formatters
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            InvalidTokensWritten.Throw(converter, tokensWritten, destination.Length);
        }

        if (
            // JIT folds into a constant
            (
                typeof(T) == typeof(bool)
                || typeof(T) == typeof(int)
                || typeof(T) == typeof(long)
                || typeof(T) == typeof(short)
                || typeof(T) == typeof(byte)
                || typeof(T) == typeof(uint)
                || typeof(T) == typeof(ulong)
                || typeof(T) == typeof(ushort)
                || typeof(T) == typeof(sbyte)
                || typeof(T) == typeof(float)
                || typeof(T) == typeof(double)
                || typeof(T) == typeof(decimal)
            ) && ReferenceEquals(Options, CsvOptions<T>._default)
        )
        {
            // default options imply Auto-quoting
            Writer.Advance(tokensWritten);
            return;
        }

        EscapeAndAdvance(destination, tokensWritten);
    }

    /// <summary>
    /// Writes the text to the writer.
    /// </summary>
    /// <param name="value">Text to write</param>
    /// <param name="skipEscaping">Don't validate, escape or quote the written value in any way</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteText(ReadOnlySpan<char> value, bool skipEscaping = false)
    {
        if (typeof(T) == typeof(char))
        {
            ReadOnlySpan<T> valueT = MemoryMarshal.Cast<char, T>(value);
            QuotingResult result = skipEscaping ? default : _quoter.NeedsQuoting(valueT);

            int tokensWritten = valueT.Length + ((2 + result.SpecialCount) & result.NeedsQuoting.ToBitwiseMask32());
            Span<T> destination = Writer.GetSpan(tokensWritten);

            if (result.NeedsQuoting)
            {
                // we use unsafe paths here; ensure writer returns long enough buffer
                _ = destination[tokensWritten - 1];

                if (result.SpecialCount == 0)
                {
                    ref T dst = ref MemoryMarshal.GetReference(destination);

                    Unsafe.CopyBlockUnaligned(
                        ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, 1)),
                        ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(valueT))),
                        sizeof(char) * (uint)valueT.Length
                    );

                    dst = _quote;
                    Unsafe.Add(ref dst, (uint)tokensWritten - 1u) = _quote;
                }
                else
                {
                    // escape value directly to the destination buffer
                    Escape.Scalar(new RFC4180Escaper<T>(_quote), valueT, destination, result.SpecialCount);
                }
            }
            else
            {
                valueT.CopyTo(destination);
            }

            Writer.Advance(tokensWritten);
        }
        else if (typeof(T) == typeof(byte))
        {
            QuotingResult result = skipEscaping ? default : _quoter.NeedsQuoting(value);
            int bytesWritten;
            int requiredLength =
                Encoding.UTF8.GetMaxByteCount(value.Length)
                + ((2 + result.SpecialCount) & result.NeedsQuoting.ToBitwiseMask32());
            Span<T> destination = Writer.GetSpan(requiredLength);

            if (result.NeedsQuoting)
            {
                // ensure writer returns correct length buffer. hopefully this elides the bounds checks later
                _ = destination[1];

                if (result.SpecialCount == 0)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(value, MemoryMarshal.Cast<T, byte>(destination).Slice(1));
                    destination[0] = _quote;
                    destination[^1] = _quote;
                }
                else
                {
                    bytesWritten = Encoding.UTF8.GetBytes(value, MemoryMarshal.Cast<T, byte>(destination).Slice(1));
                    Escape.Scalar(
                        new RFC4180Escaper<T>(_quote),
                        destination[..bytesWritten],
                        destination,
                        result.SpecialCount
                    );
                }
            }
            else
            {
                bytesWritten = Encoding.UTF8.GetBytes(value, MemoryMarshal.Cast<T, byte>(destination));
            }

            Writer.Advance(bytesWritten);
        }
        else
        {
            throw Token<T>.NotSupported;
        }
    }

    /// <summary>
    /// Writes raw value to the writer.
    /// </summary>
    /// <param name="value">Value to write</param>
    /// <param name="skipEscaping">Don't validate, escape or quote the written value in any way</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRaw(ReadOnlySpan<T> value, bool skipEscaping = false)
    {
        QuotingResult result = skipEscaping ? default : _quoter.NeedsQuoting(value);

        int tokensWritten = value.Length + ((2 + result.SpecialCount) & result.NeedsQuoting.ToBitwiseMask32());
        Span<T> destination = Writer.GetSpan(tokensWritten);

        if (result.NeedsQuoting)
        {
            // ensure writer returns correct length buffer
            _ = destination[tokensWritten - 1];

            if (result.SpecialCount == 0)
            {
                ref T dst = ref MemoryMarshal.GetReference(destination);

                Unsafe.CopyBlockUnaligned(
                    ref Unsafe.As<T, byte>(ref Unsafe.Add(ref dst, 1u)),
                    ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(value)),
                    (uint)Unsafe.SizeOf<T>() * (uint)value.Length
                );

                dst = _quote;
                Unsafe.Add(ref dst, (uint)tokensWritten - 1u) = _quote;
            }
            else
            {
                Escape.Scalar(new RFC4180Escaper<T>(_quote), value, destination, result.SpecialCount);
            }
        }
        else
        {
            value.CopyTo(destination);
        }

        Writer.Advance(tokensWritten);
    }

    /// <summary>
    /// Writes the null token for the given type to the writer.
    /// </summary>
    public void WriteNull<TValue>()
    {
        if (ReferenceEquals(Options, CsvOptions<T>._default))
        {
            // default options have an empty null
            return;
        }

        ReadOnlySpan<T> nullSpan = Options.GetNullSpan(typeof(TValue));

        if (!nullSpan.IsEmpty)
        {
            WriteRaw(nullSpan);
        }
    }

    /// <summary>
    /// Writes <see cref="CsvOptions{T}.Delimiter"/> to the writer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteDelimiter()
    {
        Span<T> destination = Writer.GetSpan(1);
        destination[0] = _delimiter;
        Writer.Advance(1);
    }

    /// <summary>
    /// Writes <see cref="CsvOptions{T}.Newline"/> to the writer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNewline()
    {
        Span<T> destination = Writer.GetSpan(2);

        // ensure destination is large enough
        _ = destination[1];

        if (typeof(T) == typeof(byte))
        {
            ReadOnlySpan<byte> data = "\n\0\r\n"u8;
            ushort value = Unsafe.ReadUnaligned<ushort>(
                ref Unsafe.Add(
                    ref Unsafe.AsRef(in data[0]),
                    (uint)(sizeof(ushort) * Unsafe.BitCast<bool, byte>(_isCRLF))
                )
            );

            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref destination[0]), value);
        }
        else if (typeof(T) == typeof(char))
        {
            ReadOnlySpan<char> data = "\n\0\r\n".AsSpan();
            uint value = Unsafe.ReadUnaligned<uint>(
                ref Unsafe.Add(
                    ref Unsafe.As<char, byte>(ref Unsafe.AsRef(in data[0])),
                    (uint)(sizeof(uint) * Unsafe.BitCast<bool, byte>(_isCRLF))
                )
            );

            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref destination[0]), value);
        }
        else
        {
            throw Token<T>.NotSupported;
        }

        Writer.Advance(1 + Unsafe.BitCast<bool, byte>(_isCRLF));
    }

    /// <summary>
    /// Advances the writer, escaping the written value if needed.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal void EscapeAndAdvance(Span<T> destination, int tokensWritten)
    {
        scoped ReadOnlySpan<T> written = destination[..tokensWritten];

        QuotingResult result = _quoter.NeedsQuoting(written);

        int advanceBy;

        if (result.NeedsQuoting)
        {
            advanceBy = tokensWritten + 2 + result.SpecialCount;

            if (advanceBy > destination.Length)
            {
                Span<T> temp = _allocator.GetSpan(tokensWritten);
                written.CopyTo(temp);
                written = temp;
                destination = Writer.GetSpan(advanceBy);
            }

            Escape.Scalar(new RFC4180Escaper<T>(_quote), written, destination, result.SpecialCount);
        }
        else
        {
            advanceBy = tokensWritten;
        }

        Writer.Advance(advanceBy);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _allocator?.Dispose();
    }
}

file static class InvalidTokensWritten
{
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(object source, int tokensWritten, int destinationLength)
    {
        throw new InvalidOperationException(
            $"{source.GetType().FullName} reported {tokensWritten} tokens written to a buffer of length {destinationLength}."
        );
    }
}
