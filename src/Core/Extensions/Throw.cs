using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FastExpressionCompiler.LightExpression;
using FlameCsv.Exceptions;

namespace FlameCsv.Extensions;

[ExcludeFromCodeCoverage]
internal static class Throw
{
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ValueTask ObjectDisposedAsync(object instance)
    {
        return ValueTask.FromException(new ObjectDisposedException(instance.GetType().FullName));
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining), StackTraceHidden]
    public static void ArgumentNull(string paramName) => throw new ArgumentNullException(paramName);

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void DefaultOrEmptyImmutableArray(string paramName = "")
    {
        throw new ArgumentException("The immutable array is default or empty.", paramName);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Argument_DefaultStruct(Type type, string? paramName = null)
    {
        throw new ArgumentException($"The struct '{type.FullName}' was uninitialized.", paramName);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidOp_DefaultStruct(Type type)
    {
        throw new InvalidOperationException($"The struct '{type.FullName}' was uninitialized.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidOp_EnumerationChanged()
    {
        throw new InvalidOperationException("The CSV enumeration state has been modified.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ObjectDisposed_Enumeration(object instance)
    {
        throw new ObjectDisposedException(instance.GetType().FullName, "The CSV enumeration has already completed.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidOperation(string message)
    {
        throw new InvalidOperationException(message);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void NotSupported_CsvHasNoHeader()
    {
        throw new NotSupportedException("The CSV does not have a header record.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidData_FieldCount(int expected, int actual)
    {
        throw new CsvReadException($"The CSV record has {actual} fields, but {expected} were expected.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidOp_NoHeader(int index, Type type, MemberInfo member)
    {
        throw new InvalidOperationException(
            $"No header name found for member {member.Name} at index {index} when writing {type.FullName}."
        );
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Argument_HeaderNameNotFound(string name, IEnumerable<string> header)
    {
        throw new ArgumentException(
            $"Header \"{name}\" was not found among the CSV headers: {string.Join(", ", header)}",
            nameof(name)
        );
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Argument_FieldIndex(int index, int? fieldCount = null, string? name = null)
    {
        string? knownFieldCount = fieldCount is not null
            ? $" (there were {fieldCount.Value} fields in the record)"
            : null;
        name = name is null ? "" : $"'{name}' ";

        throw new ArgumentOutOfRangeException($"Could not get field {name}at index {index}{knownFieldCount}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Argument_FieldIndex(int index, int count)
    {
        throw new ArgumentOutOfRangeException(
            nameof(index),
            $"Could not get field at index {index} (there were {count} fields in the record)."
        );
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Argument(string paramName, string? message)
    {
        throw new ArgumentException(message, paramName);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Config_TrueOrFalseBooleanValues(bool which)
    {
        throw new CsvConfigurationException(
            "Both true and false must be present if custom boolean values are configured ("
                + which.ToString().ToLowerInvariant()
                + " was missing)."
        );
    }

    /// <summary>
    /// Ensures the stream is readable and not null.
    /// </summary>
    public static void IfNotReadable(
        [NotNull] Stream? stream,
        [CallerArgumentExpression(nameof(stream))] string paramName = ""
    )
    {
        ArgumentNullException.ThrowIfNull(stream, paramName);

        if (!stream.CanRead)
            Argument(paramName, "Stream is not readable");
    }

    /// <summary>
    /// Ensures the stream is writable and not null.
    /// </summary>
    public static void IfNotWritable(
        [NotNull] Stream? stream,
        [CallerArgumentExpression(nameof(stream))] string paramName = ""
    )
    {
        ArgumentNullException.ThrowIfNull(stream, paramName);

        if (!stream.CanWrite)
            Argument(paramName, "Stream is not writable");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void StreamNotReadable(
        [NotNull] Stream? stream,
        [CallerArgumentExpression(nameof(stream))] string paramName = ""
    )
    {
        throw new ArgumentException("Stream.CanRead returned false", paramName);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void StreamNotWritable(
        [NotNull] Stream? stream,
        [CallerArgumentExpression(nameof(stream))] string paramName = ""
    )
    {
        throw new ArgumentException("Stream.CanWrite returned false", paramName);
    }
}
