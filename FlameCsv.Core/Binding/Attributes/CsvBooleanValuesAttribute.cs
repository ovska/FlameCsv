using System.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;
using FlameCsv.Parsers.Utf8;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the default parser for the target member. Applicable for <c>bool</c> and <c>bool?</c>
/// when parsing text or UTF8 bytes.<br/>
/// For nullable booleans, attempts to fetch user defined null token from the options via
/// <see cref="ICsvNullTokenProvider{T}"/>.
/// </summary>
public class CsvBooleanValuesAttribute : CsvParserOverrideAttribute
{
    /// <summary>
    /// Values that represent <see langword="true"/>.
    /// </summary>
    public string[] TrueValues { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Values that represent <see langword="false"/>.
    /// </summary>
    public string[] FalseValues { get; set; } = Array.Empty<string>();

    /// <inheritdoc/>
    public override ICsvParser<T> CreateParser<T>(Type targetType, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (targetType != typeof(bool) && targetType != typeof(bool?))
        {
            throw new CsvConfigurationException(
                $"{nameof(CsvBooleanValuesAttribute)} was applied on a member with invalid type: {targetType}");
        }

        if (!(TrueValues?.Length > 0 && FalseValues?.Length > 0))
        {
            throw new CsvConfigurationException($"Null/empty true/false values defined for {nameof(CsvBooleanValuesAttribute)}");
        }

        if (typeof(T) == typeof(char))
            return (ICsvParser<T>)CreateForText(targetType, (CsvReaderOptions<char>)(object)options);

        if (typeof(T) == typeof(byte))
            return (ICsvParser<T>)CreateForUtf8(targetType, (CsvReaderOptions<byte>)(object)options);

        throw new NotSupportedException($"Token type {typeof(T)} is not supported by {nameof(CsvBooleanValuesAttribute)}");
    }

    private ICsvParser<char> CreateForText(Type target, CsvReaderOptions<char> options)
    {
        var parser = new BooleanTextParser(trueValues: TrueValues, falseValues: FalseValues);

        if (target == typeof(bool))
            return parser;

        Debug.Assert(target == typeof(bool?));

        return new NullableParser<char, bool>(parser, FindNullTokens(options));
    }

    private ICsvParser<byte> CreateForUtf8(Type target, CsvReaderOptions<byte> options)
    {
        var parser = new BooleanUtf8Parser(trueValues: TrueValues, falseValues: FalseValues);

        if (target == typeof(bool))
            return parser;

        Debug.Assert(target == typeof(bool?));

        return new NullableParser<byte, bool>(parser, FindNullTokens(options));
    }

    /// <summary>
    /// Returns null tokens defined in an existing <see cref="NullableParserFactory{T}"/> if any.
    /// </summary>
    protected static ReadOnlyMemory<T> FindNullTokens<T>(CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        if (options is ICsvNullTokenProvider<T> ntp)
        {
            if (ntp.TryGetOverride(typeof(bool?), out var value) ||
                ntp.TryGetOverride(typeof(bool), out value))
            {
                return value;
            }

            return ntp.Default;
        }

        return default;
    }
}
