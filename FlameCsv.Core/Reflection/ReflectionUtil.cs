using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;

namespace FlameCsv.Reflection;

internal static class ReflectionUtil
{
    public static void ValidateParsable<T>()
    {
        if (typeof(T).IsPrimitive || typeof(T) == typeof(string) || typeof(T).IsEnum)
        {
            ThrowUnparsableException(typeof(T));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowUnparsableException(Type t)
    {
        throw new CsvReadException(
            $"The type \"{t.ToTypeString()}\" is a primitive type and cannot be read from a CSV record.");
    }

    public static bool IsTuple(Type type)
    {
        return !type.IsGenericTypeDefinition && IsTupleCore(type);
    }

    public static bool IsTuple<T>() => IsTupleCore(typeof(T));

    private static bool IsTupleCore(Type type)
    {
        return type.IsGenericType
            && type.Module == typeof(ValueTuple<>).Module
            && type.IsAssignableTo(typeof(ITuple));
    }
}
