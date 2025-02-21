using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
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
    /// Raw value of the record as a <see cref="string"/>.
    /// </summary>
    public string? RecordValue { get; set; }

    /// <summary>
    /// Raw value of the field as a <see cref="string"/>.
    /// </summary>
    public string? FieldValue { get; set; }

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

    /// <inheritdoc/>
    public override string Message
    {
        get
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack()) return base.Message;

            using var vsb = new ValueStringBuilder(stackalloc char[256]);

            vsb.Append(Message);

            bool comma = false;

            if (Line is { } line)
            {
                vsb.Append("Line: ");
                vsb.AppendFormatted(line);
                comma = true;
            }

            if (RecordPosition is { } recordPosition)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Record start position: ");
                vsb.AppendFormatted(recordPosition);
                comma = true;
            }

            if (FieldPosition is { } fieldPosition)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Field start position: ");
                vsb.AppendFormatted(fieldPosition);
                comma = true;
            }

            if (FieldIndex is { } fieldIndex)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Field index: ");
                vsb.AppendFormatted(fieldIndex);
                comma = true;
            }

            if (RecordValue is { Length: > 0 } recordValue)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Record value: [");
                vsb.Append(recordValue);
                vsb.Append(']');
                comma = true;
            }

            if (FieldValue is { Length: > 0 } fieldValue)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Field value: [");
                vsb.Append(fieldValue);
                vsb.Append(']');
            }

            return vsb.ToString();
        }
    }

    internal void Enrich<T>(int line, long position, in CsvFields<T> fields) where T : unmanaged, IBinaryInteger<T>
    {
        Line ??= line;
        RecordPosition ??= position;
        RecordValue ??= fields.Record.Span.AsPrintableString();

        if (FieldIndex is { } index && (uint)index < (uint)fields.FieldCount)
        {
            int offset =
                fields.Fields[index].GetNextStart(fields.Parser._newline.Length) -
                fields.Fields[0].GetNextStart(fields.Parser._newline.Length);

            FieldPosition ??= position + offset;
            FieldValue ??= fields.GetField(index, raw: true).AsPrintableString();
        }
    }
}
