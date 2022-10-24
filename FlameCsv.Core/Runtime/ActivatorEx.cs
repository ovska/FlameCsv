using System.Globalization;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Runtime;

internal static class ActivatorEx
{
    public static T CreateInstance<T>(Type type, params object?[] parameters) where T : class
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
