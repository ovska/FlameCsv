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
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class CsvBooleanValuesAttribute : Attribute, ICsvParserOverride
{
    public string[] TrueValues
    {
        get => _trueValues;
        set
        {
            Guard.IsNotNull(value);
            _trueValues = value;
        }
    }

    public string[] FalseValues
    {
        get => _falseValues;
        set
        {
            Guard.IsNotNull(value);
            _falseValues = value;
        }
    }

    private string[] _trueValues = Array.Empty<string>();
    private string[] _falseValues = Array.Empty<string>();

    public virtual ICsvParser<T> CreateParser<T>(CsvBinding binding, CsvReaderOptions<T> readerOptions)
        where T : unmanaged, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(readerOptions);
        Guard.IsFalse(binding.IsIgnored);

        Type target = binding.Type;

        if (target != typeof(bool) && target != typeof(bool?))
        {
            throw new CsvConfigurationException(
                $"{nameof(CsvBooleanValuesAttribute)} was applied on a member with invalid type: {binding}");
        }

        if (typeof(T) == typeof(char))
            return (ICsvParser<T>)CreateForText(target, (CsvReaderOptions<char>)(object)readerOptions);

        if (typeof(T) == typeof(byte))
            return (ICsvParser<T>)CreateForUtf8(target, (CsvReaderOptions<byte>)(object)readerOptions);

        return ThrowHelper.ThrowNotSupportedException<ICsvParser<T>>(
            $"Token type {typeof(T)} is not supported by {nameof(CsvBooleanValuesAttribute)}");
    }

    protected virtual ICsvParser<char> CreateForText(Type target, CsvReaderOptions<char> readerOptions)
    {
        var parser = new BooleanTextParser(
            TrueValues
                .Select(static t => (t, true))
                .Concat(FalseValues.Select(static t => (t, false)))
                .ToList());

        if (target == typeof(bool))
            return parser;

        return new NullableParser<char, bool>(
            parser,
            (readerOptions.TryGetParser(typeof(bool?)) as NullableParser<char, bool>)?.NullToken ?? default);
    }

    protected virtual ICsvParser<byte> CreateForUtf8(Type target, CsvReaderOptions<byte> readerOptions)
    {
        var parser = new BooleanUtf8Parser(
            TrueValues
                .Select(static t => (ToUtf8(t), true))
                .Concat(FalseValues.Select(static t => (ToUtf8(t), false)))
                .ToList());

        if (target == typeof(bool))
            return parser;

        return new NullableParser<byte, bool>(
            parser,
            (readerOptions.TryGetParser(typeof(bool?)) as NullableParser<byte, bool>)?.NullToken ?? default);
    }

    private static ReadOnlyMemory<byte> ToUtf8(string? text) => Encoding.UTF8.GetBytes(text ?? "");
}
