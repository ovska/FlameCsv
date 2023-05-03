using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    public static ReadOnlyMemory<T> SafeCopy<T>(this ReadOnlyMemory<T> data)
    {
        if (data.IsEmpty)
            return data;

        // strings are immutable and safe to return as-is
        if (typeof(T) == typeof(char) &&
            MemoryMarshal.TryGetString((ReadOnlyMemory<char>)(object)data, out _, out _, out _))
        {
            return data;
        }

        return data.ToArray();
    }

    public static T CreateInstance<T>(
        [DynamicallyAccessedMembers(Trimming.Ctors)]
        this Type type,
        params object?[] parameters) where T : class
    {
        try
        {
            var instance = Activator.CreateInstance(type, parameters)
                ?? throw new InvalidOperationException($"Instance of {type.ToTypeString()} could not be created");
            return (T)instance;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Could not create {0} from type {1} and {2} constructor parameters: [{3}]",
                    typeof(T).ToTypeString(),
                    type.ToTypeString(),
                    parameters.Length,
                    string.Join(", ", parameters.Select(o => o?.GetType().ToTypeString() ?? "<null>"))),
                innerException: e);
        }
    }
}
