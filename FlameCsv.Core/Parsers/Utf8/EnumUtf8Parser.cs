using System.Text;
using CommunityToolkit.HighPerformance.Helpers;
using FlameCsv.Extensions;

namespace FlameCsv.Parsers.Utf8;

public sealed class EnumUtf8Parser<TEnum> : ICsvParser<byte, TEnum> where TEnum : struct, Enum
{
    private static readonly Lazy<KnownValues> _knownValues = new(KnownValuesFactory);

    public bool AllowUndefinedValues { get; }
    public bool IgnoreCase { get; }

    public EnumUtf8Parser(
        bool allowUndefinedValues = false,
        bool ignoreCase = true)
    {
        AllowUndefinedValues = allowUndefinedValues;
        IgnoreCase = ignoreCase;
    }

    public bool TryParse(ReadOnlySpan<byte> span, out TEnum value)
    {
        var knownValues = _knownValues.Value;
        var hash = HashCode<byte>.Combine(span);

        if ((knownValues.Numeric?.TryGetValue(hash, out var known) ?? false)
            || (knownValues.Text?.TryGetValue(hash, out known) ?? false)
            || (knownValues.Attribute?.TryGetValue(hash, out known) ?? false))
        {
            // check value as well for possible hash collisions or duplicate [EnumMember]s
            if (span.SequenceEqual(known.Bytes))
            {
                value = known.Value;
                return true;
            }
        }

        return TryParseViaStrings(span, out value);
    }

    private bool TryParseViaStrings(ReadOnlySpan<byte> span, out TEnum value)
    {
        var stringLength = Encoding.UTF8.GetMaxCharCount(span.Length);
        var buffer = stringLength > 64
            ? new char[stringLength]
            : stackalloc char[stringLength];

        var written = Encoding.UTF8.GetChars(span, buffer);

        if (Enum.TryParse(buffer[..written], IgnoreCase, out value) && (AllowUndefinedValues || Enum.IsDefined(value)))
        {
            return true;
        }

        return false;
    }

    public bool CanParse(Type resultType)
    {
        return resultType == typeof(TEnum);
    }

    private static KnownValues KnownValuesFactory()
    {
        Dictionary<int, KnownValue> numericValues = new();
        Dictionary<int, KnownValue> stringValues = new();
        Dictionary<int, KnownValue> attributeValues = new();

        foreach (var (value, name, enumMember) in EnumExtensions.GetEnumMembers<TEnum>())
        {
            var asNumber = Encoding.UTF8.GetBytes(value.ToString("D"));
            numericValues.TryAdd(
                HashCode<byte>.Combine(asNumber),
                new KnownValue { Value = value, Bytes = asNumber });

            var asString = Encoding.UTF8.GetBytes(name);
            stringValues.TryAdd(
                HashCode<byte>.Combine(asString),
                new KnownValue { Value = value, Bytes = asString });

            if (!string.IsNullOrEmpty(enumMember))
            {
                var bytes = Encoding.UTF8.GetBytes(enumMember);
                attributeValues.TryAdd(
                    HashCode<byte>.Combine(bytes),
                    new KnownValue { Value = value, Bytes = bytes });
            }
        }

        return new KnownValues
        {
            Numeric = numericValues.Count != 0 ? numericValues : null,
            Attribute = attributeValues.Count != 0 ? attributeValues : null,
            Text = stringValues.Count != 0 ? stringValues : null,
        };
    }

    private sealed class KnownValues
    {
        public Dictionary<int, KnownValue>? Numeric { get; init; }
        public Dictionary<int, KnownValue>? Attribute { get; init; }
        public Dictionary<int, KnownValue>? Text { get; init; }
    }

#pragma warning disable CS8618
    private sealed class KnownValue
    {
        /// <summary>Enum value used to compare against hash collisions.</summary>
        public TEnum Value { get; init; }

        /// <summary>Name of the enum as UTF8 bytes.</summary>
        public byte[] Bytes { get; init; }
    }
#pragma warning restore CS8618
}
