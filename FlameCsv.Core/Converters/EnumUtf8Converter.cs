using System.Collections.Concurrent;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;
using CommunityToolkit.HighPerformance.Helpers;
using FlameCsv.Extensions;

namespace FlameCsv.Converters;

public sealed class EnumUtf8Converter<TEnum>
    : CsvConverter<byte, TEnum> where TEnum : struct, Enum
{
    private readonly bool _allowUndefinedValues;
    private readonly bool _ignoreCase;

    private readonly ConcurrentDictionary<int, (ReadOnlyMemory<byte> bytes, TEnum value)> _readCache;
    private readonly ConcurrentDictionary<TEnum, ReadOnlyMemory<byte>> _writeCache;

    public EnumUtf8Converter(CsvOptions<byte> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowUndefinedValues = options.AllowUndefinedEnumValues;
        _ignoreCase = options.IgnoreEnumCase;
        _readCache = new();
        _writeCache = new();
    }

    public override bool TryParse(ReadOnlySpan<byte> source, out TEnum value)
    {
        int hashCode = HashCode<byte>.Combine(source);
        bool skipCache = false;

        if (_readCache.TryGetValue(hashCode, out var cached))
        {
            var (bytes, _value) = cached;

            if (source.SequenceEqual(bytes.Span))
            {
                value = _value;
                return true;
            }

            skipCache = true; // key already exists
        }

        int maxLength = Encoding.UTF8.GetMaxCharCount(source.Length);

        if (Token<char>.CanStackalloc(maxLength))
        {
            Span<char> chars = stackalloc char[maxLength];
            int written = Encoding.UTF8.GetChars(source, chars);
            return TryParseCore(hashCode, source, chars[..written], out value, skipCache);
        }
        else
        {
            using var owner = SpanOwner<char>.Allocate(maxLength);
            Span<char> chars = owner.Span;
            int written = Encoding.UTF8.GetChars(source, chars);
            return TryParseCore(hashCode, source, chars[..written], out value, skipCache);
        }
    }

    public override bool TryFormat(Span<byte> destination, TEnum value, out int charsWritten)
    {
        if (!_writeCache.TryGetValue(value, out ReadOnlyMemory<byte> name))
        {
            name = Encoding.UTF8.GetBytes(value.ToString());

            if (_writeCache.Count <= 64)
            {
                _writeCache.TryAdd(value, name);
            }
        }

        return name.Span.TryWriteTo(destination, out charsWritten);
    }

    private bool TryParseCore(
        int hashCode,
        ReadOnlySpan<byte> sourceBytes,
        ReadOnlySpan<char> sourceChars,
        out TEnum value,
        bool skipCache)
    {
        if (Enum.TryParse(sourceChars, _ignoreCase, out value) &&
            (_allowUndefinedValues || Enum.IsDefined(value)))
        {
            if (!skipCache && _readCache.Count <= 64)
            {
                _readCache.TryAdd(hashCode, (sourceBytes.ToArray(), value));
            }

            return true;
        }

        value = default;
        return false;
    }
}
