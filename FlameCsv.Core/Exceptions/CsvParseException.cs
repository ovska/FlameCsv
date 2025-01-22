using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents an error of an unparseable value.
/// </summary>
/// <remarks>
/// Initializes an exception representing an unparseable value.
/// </remarks>
public sealed class CsvParseException(
    string? message = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Parser instance.
    /// </summary>
    public object? Converter { get; set; }

    /// <summary>
    /// Throws an exception for a field that could not be parsed.
    /// </summary>
    /// <param name="value">Field value</param>
    /// <param name="converter">Converter used</param>
    /// <param name="target"></param>
    /// <exception cref="CsvParseException"></exception>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw<T, TValue>(ReadOnlySpan<T> value, CsvConverter<T, TValue> converter, string target)
        where T : unmanaged, IBinaryInteger<T>
    {
        throw new CsvParseException(
            $"Failed to parse {typeof(TValue).FullName} {target} using {converter.GetType().FullName} from {value.AsPrintableString()}")
        {
            Converter = converter
        };
    }
}
