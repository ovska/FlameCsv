using System.Text;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;
using FlameCsv.Parsers.Utf8;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the default parser for the target member. Applicable for <c>bool</c> and <c>bool?</c>.
/// For nullable booleans, attempts to fetch user defined null token from <see cref="CsvReaderOptions{T}"/>.
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
    public override ICsvParser<T> CreateParser<T>(CsvBinding binding, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Guard.IsFalse(binding.IsIgnored);

        Type target = binding.Type;

        if (target != typeof(bool) && target != typeof(bool?))
        {
            throw new CsvConfigurationException(
                $"{nameof(CsvBooleanValuesAttribute)} was applied on a member with invalid type: {binding}");
        }

        if (!(TrueValues?.Length > 0 && FalseValues?.Length > 0))
        {
            throw new CsvConfigurationException($"Null/empty true/false values defined for {nameof(CsvBooleanValuesAttribute)}");
        }

        if (typeof(T) == typeof(char))
            return (ICsvParser<T>)CreateForText(target, (CsvReaderOptions<char>)(object)options);

        if (typeof(T) == typeof(byte))
            return (ICsvParser<T>)CreateForUtf8(target, (CsvReaderOptions<byte>)(object)options);

        return ThrowHelper.ThrowNotSupportedException<ICsvParser<T>>(
            $"Token type {typeof(T)} is not supported by {nameof(CsvBooleanValuesAttribute)}");
    }

    private ICsvParser<char> CreateForText(Type target, CsvReaderOptions<char> options)
    {
        var parser = new BooleanTextParser(
            TrueValues
                .Select(static t => (t, true))
                .Concat(FalseValues.Select(static t => (t, false)))
                .ToList());

        if (target == typeof(bool))
            return parser;

        return new NullableParser<char, bool>(parser, FindNullTokens(options));
    }

    private ICsvParser<byte> CreateForUtf8(Type target, CsvReaderOptions<byte> options)
    {
        var parser = new BooleanUtf8Parser(
            TrueValues
                .Select(static t => (ToUtf8(t), true))
                .Concat(FalseValues.Select(static t => (ToUtf8(t), false)))
                .ToList());

        if (target == typeof(bool))
            return parser;

        return new NullableParser<byte, bool>(parser, FindNullTokens(options));
    }

    /// <summary>
    /// Returns null tokens defined in an existing <see cref="NullableParserFactory{T}"/> if any.
    /// </summary>
    protected static ReadOnlyMemory<T> FindNullTokens<T>(CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        foreach (var parser in options.EnumerateParsers())
        {
            if (parser is NullableParserFactory<T> nullableParserFactory)
            {
                return nullableParserFactory.NullToken;
            }
        }

        return default;
    }

    private static ReadOnlyMemory<byte> ToUtf8(string? text) => Encoding.UTF8.GetBytes(text ?? "");
}
