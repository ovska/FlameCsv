using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
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
public sealed class CsvParseException(string? message = null, Exception? innerException = null)
    : CsvReadExceptionBase(message, innerException)
{
    /// <summary>
    /// Converter instance.
    /// </summary>
    public object? Converter { get; set; }

    /// <summary>
    /// Index of the field in the record.
    /// </summary>
    public int? FieldIndex { get; set; }

    /// <summary>
    /// If available, name of the target property, field, or parameter.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Target type of the conversion.
    /// </summary>
    public Type? TargetType { get; set; }

    /// <summary>
    /// Start position of the field where the exception occurred.
    /// </summary>
    public long? FieldPosition { get; set; }

    /// <summary>
    /// Raw value of the field as a <see cref="string"/>.
    /// </summary>
    public string? FieldValue { get; set; }

    /// <summary>
    /// Header of the field where the exception occurred.
    /// </summary>
    public string? HeaderValue { get; set; }

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
            Converter = converter,
            FieldIndex = fieldIndex,
            Target = target,
            TargetType = parsedType,
        };
    }

    /// <inheritdoc/>
    public override string Message
    {
        get
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                return base.Message;
            }

            using var vsb = new ValueStringBuilder(stackalloc char[256]);

            vsb.Append(base.Message);

            bool comma = false;

            if (FieldValue is { Length: > 0 } fieldValue)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Field value: [");
                vsb.Append(fieldValue);
                vsb.Append(']');
            }

            if (FieldIndex is { } fieldIndex)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Field index: ");
                vsb.AppendFormatted(fieldIndex);
                comma = true;
            }

            if (HeaderValue is { } header)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Header: [");
                vsb.Append(header);
                vsb.Append(']');
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

            if (Line is { } line)
            {
                vsb.Append(" Line: ");
                vsb.AppendFormatted(line);
                comma = true;
            }

            if (FieldPosition is { } fieldPosition)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Field start position: ");
                vsb.AppendFormatted(fieldPosition);
                comma = true;
            }

            if (RecordPosition is { } recordPosition)
            {
                vsb.Append(comma ? ", " : " ");
                vsb.Append("Record start position: ");
                vsb.AppendFormatted(recordPosition);
            }

            return vsb.ToString();
        }
    }

    internal void WithHeader(ImmutableArray<string> headers)
    {
        if (!headers.IsDefault && FieldIndex is { } index && (uint)index < (uint)headers.Length)
        {
            HeaderValue = headers[index];
        }
    }

    internal override void Enrich<T>(int line, long position, CsvRecordRef<T> record)
    {
        base.Enrich(line, position, record);

        if (FieldIndex is { } index && (uint)index < (uint)record.FieldCount)
        {
            int recordStart = (int)(uint)record._bits[0];
            int fieldStart = (int)(uint)record._bits[index];
            int offset = fieldStart - recordStart;

            FieldPosition ??= position + offset;
            FieldValue ??= record[index].AsPrintableString();
        }
    }
}
