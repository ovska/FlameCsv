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
    public static void Throw<T>(ReadOnlySpan<T> value, CsvConverter<T>? converter = null, string? target = null)
        where T : unmanaged, IBinaryInteger<T>
    {
        string info = "";

        if (target is not null)
        {
            info += $" target \"{target}\"";
        }

        if (converter is not null)
        {
            info += $" using {converter.GetType().FullName}";
        }


        throw new CsvParseException($"Failed to parse{info} from {value.AsPrintableString()}.")
        {
            Converter = converter,
        };
    }

    public static void Throw<T>(ReadOnlySpan<T> value, (CsvConverter<T>? converter, string? target) info)
        where T : unmanaged, IBinaryInteger<T>
    {
        Throw(value, info.converter, info.target);
    }
}
