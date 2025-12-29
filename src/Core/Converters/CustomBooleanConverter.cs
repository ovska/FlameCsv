using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Utilities.Comparers;

namespace FlameCsv.Converters;

internal sealed class CustomBooleanConverter<T> : CsvConverter<T, bool>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly object _firstTrue;
    private readonly object _firstFalse;
    private readonly ImmutableArray<(int hash, string value)> _trueValues;
    private readonly ImmutableArray<(int hash, string value)> _falseValues;
    private readonly IAlternateEqualityComparer<ReadOnlySpan<T>, string> _comparer;

    internal CustomBooleanConverter(CsvOptions<T> options)
        : this(
            options.BooleanValues.Where(v => v.value).Select(v => v.text),
            options.BooleanValues.Where(v => !v.value).Select(v => v.text),
            options.IgnoreHeaderCase
        ) { }

    internal CustomBooleanConverter(IEnumerable<string> trueValues, IEnumerable<string> falseValues, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(trueValues);
        ArgumentNullException.ThrowIfNull(falseValues);

        // ReSharper disable PossibleMultipleEnumeration

        _comparer = GetComparer(trueValues.Concat(falseValues), ignoreCase);

        var trueBuilder = ImmutableArray.CreateBuilder<(int hash, string value)>();
        var falseBuilder = ImmutableArray.CreateBuilder<(int hash, string value)>();

        var cmp = (IEqualityComparer<string>)_comparer;

        foreach (string text in trueValues)
        {
            int hash = cmp.GetHashCode(text);
            trueBuilder.Add((hash, text));
            _firstTrue ??= GetT(text);
        }

        foreach (string text in falseValues)
        {
            int hash = cmp.GetHashCode(text);
            falseBuilder.Add((hash, text));
            _firstFalse ??= GetT(text);
        }

        // ReSharper restore PossibleMultipleEnumeration

        if (_firstTrue is null)
            Throw.Config_TrueOrFalseBooleanValues(true);

        if (_firstFalse is null)
            Throw.Config_TrueOrFalseBooleanValues(false);

        _trueValues = trueBuilder.ToImmutable();
        _falseValues = falseBuilder.ToImmutable();
    }

    public override bool TryFormat(Span<T> destination, bool value, out int charsWritten)
    {
        ReadOnlySpan<T> span = value ? GetFromT(_firstTrue) : GetFromT(_firstFalse);

        if (span.TryCopyTo(destination))
        {
            charsWritten = span.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    public override bool TryParse(ReadOnlySpan<T> source, out bool value)
    {
        int hashCode = _comparer.GetHashCode(source);

        foreach ((int hash, string text) in _trueValues)
        {
            if (hash == hashCode && _comparer.Equals(source, text))
            {
                value = true;
                return true;
            }
        }

        foreach ((int hash, string text) in _falseValues)
        {
            if (hash == hashCode && _comparer.Equals(source, text))
            {
                value = false;
                return true;
            }
        }

        value = false;
        return false;
    }

    private static IAlternateEqualityComparer<ReadOnlySpan<T>, string> GetComparer(
        IEnumerable<string> values,
        bool ignoreCase
    )
    {
        if (typeof(T) == typeof(char))
        {
            object cmp = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            return (IAlternateEqualityComparer<ReadOnlySpan<T>, string>)cmp;
        }

        if (typeof(T) != typeof(byte))
        {
            throw Token<T>.NotSupported;
        }

        bool allAscii = values.All(s => Ascii.IsValid(s));

        return (IAlternateEqualityComparer<ReadOnlySpan<T>, string>)
            (object)(
                (ignoreCase, allAscii) switch
                {
                    (true, true) => IgnoreCaseAsciiComparer.Instance,
                    (false, true) => OrdinalAsciiComparer.Instance,
                    (true, false) => Utf8Comparer.OrdinalIgnoreCase,
                    (false, false) => Utf8Comparer.Ordinal,
                }
            );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<T> GetFromT(object value)
    {
        if (typeof(T) == typeof(char))
        {
            Check.True(value is string, $"Expected string but was {value.GetType().FullName}");
            return Unsafe.As<string>(value).AsSpan().Cast<char, T>();
        }

        if (typeof(T) == typeof(byte))
        {
            Check.True(value is byte[], $"Expected byte[] but was {value.GetType().FullName}");
            return Unsafe.As<byte[]>(value).AsSpan().Cast<byte, T>();
        }

        throw Token<T>.NotSupported;
    }

    private static object GetT(string value)
    {
        if (typeof(T) == typeof(char))
        {
            return value;
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.GetBytes(value);
        }

        throw Token<T>.NotSupported;
    }
}
