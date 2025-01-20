using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv.Converters;

internal sealed class CustomBooleanUtf8Converter : CsvConverter<byte, bool>
{
    private readonly ImmutableArray<(bool value, byte[] bytes)> _values;
    private readonly byte[] _firstTrue;
    private readonly byte[] _firstFalse;
    private readonly bool _ignoreCase;
    private readonly bool _allAscii;

    internal CustomBooleanUtf8Converter(CsvOptions<byte> options)
    {
        if (options._booleanValues is not { Count: > 0 })
            Throw.Argument(nameof(CsvOptions<byte>.BooleanValues), "No values defined");

        var values = options._booleanValues;

        List<byte[]> trues = new((values.Count / 2) + 1);
        List<byte[]> falses = new((values.Count / 2) + 1);

        foreach ((string text, bool value) in values)
        {
            (value ? trues : falses).Add(options.GetFromString(text).UnsafeGetOrCreateArray());
        }

        if (trues.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (falses.Count == 0)
            Throw.Config_TrueOrFalseBooleanValues(false);

        if (ReferenceEquals(options.Comparer, StringComparer.OrdinalIgnoreCase))
        {
            _ignoreCase = true;
        }

        if (!ReferenceEquals(options.Comparer, StringComparer.Ordinal))
        {
            throw new CsvConfigurationException(
                "Utf8 BooleanValues is only supported with Comparer Ordinal or OrdinalIgnoreCase");
        }

        InitializeTrueAndFalse(_values, out _firstTrue, out _firstFalse, out _allAscii);
    }

    internal CustomBooleanUtf8Converter(
        CsvOptions<byte> options,
        string[] trueValues,
        string[] falseValues,
        bool ignoreCase)
    {
        Dictionary<string, bool> uniqueValues
            = new(ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (string trueValue in trueValues)
        {
            uniqueValues.TryAdd(trueValue ?? "", true);
        }

        foreach (string falseValue in falseValues)
        {
            uniqueValues.TryAdd(falseValue ?? "", false);
        }

        _values = [..uniqueValues.Select(v => (v.Value, options.GetFromString(v.Key).UnsafeGetOrCreateArray()))];
        _ignoreCase = ignoreCase;
        InitializeTrueAndFalse(_values, out _firstTrue, out _firstFalse, out _allAscii);
    }

    public override bool TryFormat(Span<byte> destination, bool value, out int charsWritten)
    {
        return ((ReadOnlySpan<byte>)(value ? _firstTrue : _firstFalse)).TryCopyTo(destination, out charsWritten);
    }

    /// <inheritdoc/>
    public override bool TryParse(ReadOnlySpan<byte> source, out bool value)
    {
        if (_ignoreCase)
        {
            if (_allAscii && Ascii.IsValid(source))
            {
                foreach ((bool result, byte[] bytes) in _values)
                {
                    if (Ascii.EqualsIgnoreCase(source, bytes))
                    {
                        value = result;
                        return true;
                    }
                }
            }

            foreach ((bool result, byte[] bytes) in _values)
            {
                if (Utf8Util.SequenceEqualSlow(source, bytes, StringComparison.OrdinalIgnoreCase))
                {
                    value = result;
                    return true;
                }
            }
        }

        foreach ((bool result, byte[] bytes) in _values)
        {
            if (source.SequenceEqual(bytes))
            {
                value = result;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static void InitializeTrueAndFalse(
        ImmutableArray<(bool value, byte[] bytes)> values,
        [NotNull] out byte[]? firstTrue,
        [NotNull] out byte[]? firstFalse,
        out bool allAscii)
    {
        firstTrue = null;
        firstFalse = null;
        allAscii = true;

        foreach ((bool value, byte[] bytes) in values)
        {
            if (firstTrue is null && value)
            {
                firstTrue = bytes;
            }

            if (firstFalse is null && !value)
            {
                firstFalse = bytes;
            }

            if (allAscii && !Ascii.IsValid(bytes))
            {
                allAscii = false;
            }
        }

        if (firstTrue is null)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (firstFalse is null)
            Throw.Config_TrueOrFalseBooleanValues(false);
    }
}
