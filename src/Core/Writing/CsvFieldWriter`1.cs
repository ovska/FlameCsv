using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;
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
public readonly struct CsvFieldWriter<T> : IDisposable, ParallelUtils.IConsumable
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
    private readonly IQuoter<T> _quoter;
    private readonly Allocator<T> _allocator;
    private readonly object? _defaultStringConverter;
    private readonly bool _usesDefaultOptions;

    /// <summary>
    /// Newline value that can be written as a single unit, e.g. <c>'\r' | ('\n' &lt;&lt; 16)</c> for <see cref="char"/> and <c>0x0A0D</c> for <see cref="byte"/>.
    /// </summary>
    private readonly uint _newlineValue;
    private readonly int _newlineLength;

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
        _quote = T.CreateTruncating(options.Quote.GetValueOrDefault());
        _quoter = Quoter.Create(options);
        _newlineValue = Bithacks.InitializeCRLFRegister<T>(options.Newline.IsCRLF());
        _newlineLength = options.Newline.IsCRLF() ? 2 : 1;
        _allocator = new Allocator<T>(writer.BufferPool);
        _defaultStringConverter =
            typeof(T) == typeof(char) ? Converters.StringTextConverter.Instance
            : typeof(T) == typeof(byte) ? Converters.StringUtf8Converter.Instance
            : null;
        _usesDefaultOptions = ReferenceEquals(options, CsvOptions<T>.Default);
    }

    /// <summary>
    /// Writes <paramref name="value"/> to the writer using <paramref name="converter"/>.
    /// </summary>
    public void WriteField<TValue>(CsvConverter<T, TValue> converter, TValue? value)
    {
        // string is super common and most likely not overridden (IsValueType is intrinsic for generics)
        // use a local to avoid static ctor call on every WriteField
        if (!typeof(TValue).IsValueType && ReferenceEquals(_defaultStringConverter, converter))
        {
            Check.True(typeof(TValue) == typeof(string), $"Invalid converter {converter} for type {typeof(TValue)}");
            WriteText(Unsafe.As<string?>(value));
            return;
        }

        int tokensWritten;
        scoped Span<T> destination;

        // null check is folded away for value types
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
            if (_usesDefaultOptions)
            {
                // default options have an empty null
                return;
            }

            ReadOnlySpan<T> nullValue = Options.GetNullObject(typeof(TValue)).AsSpan<T>();

            if (!nullValue.IsEmpty)
            {
                destination = Writer.GetSpan(nullValue.Length);
                nullValue.CopyTo(destination);
                tokensWritten = nullValue.Length;
            }
            else
            {
                tokensWritten = nullValue.Length;
                destination = [];
            }
        }

        // fast path: default options with primitives; these never need validation or escaping
        if (
            // JIT folds struct typeof checks into a constant
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
                || typeof(T) == typeof(Guid)
            ) && _usesDefaultOptions
        )
        {
            Writer.Advance(tokensWritten);
            return;
        }

        // validate negative or too large tokensWritten in case of broken user-defined formatters
        if ((uint)tokensWritten > (uint)destination.Length)
        {
            InvalidTokensWritten.Throw(converter, tokensWritten, destination.Length);
        }

        EscapeAndAdvance(destination, tokensWritten);
    }

    /// <summary>
    /// Writes the text to the writer.
    /// </summary>
    /// <param name="value">Text to write</param>
    /// <param name="skipEscaping">Don't quote or escape the value</param>
    public void WriteText(ReadOnlySpan<char> value, bool skipEscaping = false)
    {
        if (value.IsEmpty)
        {
            return;
        }

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
                    Escape.Scalar(_quote, valueT, destination, result.SpecialCount);
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
                if (result.SpecialCount == 0)
                {
                    bytesWritten = Encoding.UTF8.GetBytes(
                        value,
                        Unsafe.BitCast<Span<T>, Span<byte>>(destination).Slice(1)
                    );
                    destination[0] = _quote;
                    destination[bytesWritten + 1] = _quote;
                }
                else
                {
                    bytesWritten = Encoding.UTF8.GetBytes(value, Unsafe.BitCast<Span<T>, Span<byte>>(destination));
                    Escape.Scalar(_quote, destination[..bytesWritten], destination, result.SpecialCount);
                }

                bytesWritten += (2 + result.SpecialCount);
            }
            else
            {
                bytesWritten = Encoding.UTF8.GetBytes(value, Unsafe.BitCast<Span<T>, Span<byte>>(destination));
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
        if (value.IsEmpty)
        {
            return;
        }

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
                Escape.Scalar(_quote, value, destination, result.SpecialCount);
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
        if (_usesDefaultOptions)
        {
            // default options have an empty null
            return;
        }

        ReadOnlySpan<T> nullSpan = Options.GetNullObject(typeof(TValue)).AsSpan<T>();

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
            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref destination[0]), (ushort)_newlineValue);
        }
        else if (typeof(T) == typeof(char))
        {
            Unsafe.WriteUnaligned(ref Unsafe.As<T, byte>(ref destination[0]), _newlineValue);
        }
        else
        {
            throw Token<T>.NotSupported;
        }

        Writer.Advance(_newlineLength);
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

            Escape.Scalar(_quote, written, destination, result.SpecialCount);
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

    bool ParallelUtils.IConsumable.ShouldConsume => Writer.NeedsFlush;
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
