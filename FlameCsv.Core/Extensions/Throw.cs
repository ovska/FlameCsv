using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;

namespace FlameCsv.Extensions;

internal static class Throw
{
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfEnumerationChanged(int version, int expected)
    {
        if (expected == -1)
            ObjectDisposed_Enumeration();

        if (version != expected)
            InvalidOp_EnumerationChanged();
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void InvalidOp_EnumerationChanged()
    {
        throw new InvalidOperationException("The CSV enumeration state has been modified.");
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
    public static void Unreachable(ref DefaultInterpolatedStringHandler handler)
    {
        throw new UnreachableException(handler.ToStringAndClear());
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
        throw new ArgumentException($"Header \"{name}\" was not found among the CSV headers: {string.Join(", ", header)}", nameof(name));
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void ParseFailed<T>(ReadOnlySpan<T> field, CsvConverter<T> converter, Type toParse)
            where T : unmanaged, IBinaryInteger<T>
    {
        throw new CsvParseException(
            $"Failed to parse {toParse.FullName} using {converter.GetType().FullName} " +
            $"from {field.AsPrintableString()}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Argument_FieldIndex<T>(int index, EnumeratorState<T>? state = null, string? name = null)
            where T : unmanaged, IBinaryInteger<T>
    {
        string? knownFieldCount = null;
        name = name is null ? "" : $"'{name}' ";
        Exception? inner = null;

        if (state is not null)
        {
            try
            {
                knownFieldCount = $" (there were {state.GetFieldCount()} fields in the record)";
            }
            catch (Exception e)
            {
                knownFieldCount = " (could not determine the number of fields in the record, see inner exception for details)";
                inner = e;
            }
        }

        throw new ArgumentOutOfRangeException($"Could not get field {name}at index {index}{knownFieldCount}.", inner);
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

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Config_TrueOrFalseBooleanValues(bool which)
    {
        throw new CsvConfigurationException(
            $"If {nameof(CsvOptions<byte>.BooleanValues)} it not empty, it must contain at least one value " +
            $"for both true and false ({which.ToString().ToLowerInvariant()} was missing).");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static int Argument_FieldName(string name)
    {
        throw new KeyNotFoundException($"Header field '{name}' was not found.");
    }
}
