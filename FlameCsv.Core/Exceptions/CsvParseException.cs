using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents an error of an unparseable value.
/// </summary>
public sealed class CsvParseException : Exception
{
    /// <summary>
    /// Parser instance.
    /// </summary>
    public object? Converter { get; set; }

    /// <summary>
    /// Initializes an exception representing an unparseable value.
    /// </summary>
    public CsvParseException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw<T>(
        CsvOptions<T> options,
        ReadOnlySpan<T> value,
        CsvConverter<T>? converter = null)
        where T : unmanaged, IEquatable<T>
    {
        string withStr = converter is null ? "" : $" with {converter.GetType()}";

        throw new CsvParseException($"Failed to parse{withStr} from {options.AsPrintableString(value.ToArray())}.")
        {
            Converter = converter,
        };
    }
}
