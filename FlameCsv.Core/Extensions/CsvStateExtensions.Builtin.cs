using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;

namespace FlameCsv.Extensions;

internal static partial class CsvStateExtensions
{
    private static bool TryGetTupleBindings<T, TTuple>([NotNullWhen(true)] out CsvBindingCollection<TTuple>? bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        if (!IsTuple<TTuple>())
        {
            bindingCollection = null;
            return false;
        }

        var parameters = typeof(TTuple).GetConstructors()[0].GetParameters();
        var bindingsList = new List<CsvBinding>(parameters.Length);

        // TODO: add support for ignored columns via a special type, e.g. struct CsvIgnore { }
        for (int i = 0; i < parameters.Length; i++)
        {
            bindingsList.Add(new CsvBinding(i, parameters[i]));
        }

        bindingCollection = new CsvBindingCollection<TTuple>(bindingsList, isInternalCall: true);
        return true;
    }

    static bool IsTuple<T>()
    {
        if (typeof(T).IsGenericType && !typeof(T).IsGenericTypeDefinition)
        {
            Type g = typeof(T).GetGenericTypeDefinition();

            return g == typeof(ValueTuple<>)
                || g == typeof(ValueTuple<,>)
                || g == typeof(ValueTuple<,,>)
                || g == typeof(ValueTuple<,,,>)
                || g == typeof(ValueTuple<,,,,>)
                || g == typeof(ValueTuple<,,,,,>)
                || g == typeof(ValueTuple<,,,,,,>)
                || g == typeof(ValueTuple<,,,,,,,>)
                || g == typeof(Tuple<>)
                || g == typeof(Tuple<,>)
                || g == typeof(Tuple<,,>)
                || g == typeof(Tuple<,,,>)
                || g == typeof(Tuple<,,,,>)
                || g == typeof(Tuple<,,,,,>)
                || g == typeof(Tuple<,,,,,,>)
                || g == typeof(Tuple<,,,,,,,>);
        }

        return false;
    }
}

