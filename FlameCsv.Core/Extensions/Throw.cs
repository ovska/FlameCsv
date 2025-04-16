using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;

namespace FlameCsv.Extensions;

internal static class Throw
{
    [StackTraceHidden]
    public static void IfDefaultOrEmpty(
        ImmutableArray<string> array,
        [CallerArgumentExpression(nameof(array))] string paramName = "")
    {
        if (!array.IsDefaultOrEmpty)
            return;

        Argument(paramName, "The array is default or empty.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfDefaultStruct([DoesNotReturnIf(true)] bool signal, Type type)
    {
        if (!signal)
            return;

        InvalidOp_DefaultStruct(type);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidOp_DefaultStruct(Type type)
    {
        throw new InvalidOperationException($"The struct '{type.FullName}' was uninitialized.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfEnumerationDisposed([DoesNotReturnIf(true)] bool disposed)
    {
        if (!disposed)
            return;

        ObjectDisposed_Enumeration();
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidOp_EnumerationChanged()
    {
        throw new InvalidOperationException("The CSV enumeration state has been modified.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void NotSupported_SyncRead(object reader)
    {
        throw new NotSupportedException($"{reader.GetType().FullName} does not support synchronous reads.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ObjectDisposed_Enumeration()
    {
        throw new ObjectDisposedException(null, "The CSV enumeration has already completed.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Unreachable_AlreadyHasHeader()
    {
        throw new UnreachableException("The header record has already been read.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Unreachable(string? message)
    {
        throw new UnreachableException(message);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidOperation_HeaderNotRead()
    {
        throw new InvalidOperationException("The CSV header has not been read.");
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
            $"No header name found for member {member.Name} at index {index} when writing {type.FullName}.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Argument_HeaderNameNotFound(string name, IEnumerable<string> header)
    {
        throw new ArgumentException(
            $"Header \"{name}\" was not found among the CSV headers: {string.Join(", ", header)}",
            nameof(name));
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
            $"Could not get field at index {index} (there were {count} fields in the record).");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Argument_OutOfRange(string paramName)
    {
        throw new ArgumentOutOfRangeException(paramName);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Argument(string paramName, string? message)
    {
        throw new ArgumentException(message, paramName);
    }

    public static void IfInvalidArgument(
        [DoesNotReturnIf(true)] bool condition,
        string message,
        [CallerArgumentExpression(nameof(condition))]
        string paramName = "")
    {
        if (!condition)
            return;

        Argument(paramName, message);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Config_TrueOrFalseBooleanValues(bool which)
    {
        throw new CsvConfigurationException(
            $"If {nameof(CsvOptions<byte>.BooleanValues)} it not empty, it must contain at least one value " +
            $"for both true and false ({which.ToString().ToLowerInvariant()} was missing).");
    }
}
