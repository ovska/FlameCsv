using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Enumeration;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents an error of an unparseable value.
/// </summary>
/// <remarks>
/// Initializes an exception representing an unparseable value.
/// </remarks>
[PublicAPI]
public sealed class CsvParseException(
    string? message = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Converter instance.
    /// </summary>
    public object? Converter { get; set; }

    /// <summary>
    /// Index of the field in the record.
    /// </summary>
    public int? FieldIndex { get; init; }

    /// <summary>
    /// If available, name of the target property, field, or parameter.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Target type of the conversion.
    /// </summary>
    public Type? TargetType { get; init; }

    /// <inheritdoc/>
    public override string Message
        => string.IsNullOrEmpty(AdditionalMessage)
            ? base.Message
            : $"{base.Message}{Environment.NewLine}{AdditionalMessage}";

    internal string? AdditionalMessage { get; set; }

    /// <summary>
    /// Line of the record where the exception occurred.
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Start position of the record where the exception occurred.
    /// </summary>
    public long? RecordPosition { get; set; }

    /// <summary>
    /// Start position of the field where the exception occurred.
    /// </summary>
    public long? FieldPosition { get; set; }

    /// <summary>
    /// Throws an exception for a field that could not be parsed.
    /// </summary>
    /// <param name="fieldIndex">Index of the field in the record</param>
    /// <param name="parsedType">Converted type</param>
    /// <param name="converter">Converter used</param>
    /// <param name="target">If available, name of the target</param>
    /// <exception cref="CsvParseException"></exception>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Throw(int fieldIndex, Type parsedType, object converter, string target)
    {
        throw new CsvParseException($"Failed to parse {parsedType.Name} {target} using {converter.GetType().Name}.")
        {
            Converter = converter, FieldIndex = fieldIndex, Target = target,
        };
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ThrowInternal<T>(
        int fieldIndex,
        Type parsedType,
        object converter,
        string target,
        ref readonly CsvFields<T> fields,
        CsvEnumeratorBase<T> enumerator) where T : unmanaged, IBinaryInteger<T>
    {
        var ex = new CsvParseException($"Failed to parse {parsedType.Name} {target} using {converter.GetType().Name}.")
        {
            Converter = converter, FieldIndex = fieldIndex, Target = target,
        };
        enumerator.EnrichParseException(ex, in fields);
        throw ex;
    }
}
