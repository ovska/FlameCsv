using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using DAMT = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

namespace FlameCsv.Reflection;

internal static class MaterializerExtensions
{
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    private static class ForType<T, TResult>
        where T : unmanaged, IBinaryInteger<T>
    {
        /// <summary>
        /// Cached bindings for headerless CSV.
        /// </summary>
        public static DelegateGenerator<T>.MaterializerFactory<TResult>? Cached;
    }

    /// <summary>
    /// Creates a materializer from the bindings.
    /// </summary>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public static IMaterializer<T, TResult> CreateMaterializerFrom<T, [DAM(Messages.ReflectionBound)] TResult>(
        this CsvOptions<T> options,
        CsvBindingCollection<TResult> bindingCollection
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        return ExpressionDelegateGenerator<T>.Instance.GetMaterializerFactory(bindingCollection)(options);
    }

    /// <summary>
    /// Binds the options using built-in or index binding.
    /// </summary>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public static IMaterializer<T, TResult> GetMaterializerNoHeader<T, [DAM(Messages.ReflectionBound)] TResult>(
        this CsvOptions<T> options
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        DelegateGenerator<T>.MaterializerFactory<TResult>? factory = ForType<T, TResult>.Cached;

        if (factory is null)
        {
            if (
                TryGetTupleBindings<T, TResult>(write: false, out var bindings)
                || IndexAttributeBinder<TResult>.TryGetBindings(write: false, out bindings)
            )
            {
                factory = ExpressionDelegateGenerator<T>.Instance.GetMaterializerFactory(bindings);
            }
            else
            {
                // Don't cache nulls since its unlikely they will be attempted many times
                throw new CsvBindingException<TResult>(
                    $"Headerless CSV could not be bound to {typeof(TResult)}, since the type had no "
                        + "[CsvIndex]-attributes and no built-in configuration."
                );
            }

            factory = Interlocked.CompareExchange(ref ForType<T, TResult>.Cached, factory, null) ?? factory;

            HotReloadService.RegisterForHotReload(factory, static _ => ForType<T, TResult>.Cached = null);
        }

        return factory(options);
    }

    internal static bool IsTuple(Type type)
    {
        return !type.IsGenericTypeDefinition
            && type.IsGenericType
            && type.Module == typeof(ValueTuple<>).Module
            && type.IsAssignableTo(typeof(ITuple));
    }

    internal static bool TryGetTupleBindings<T, [DAM(DAMT.PublicConstructors | DAMT.PublicFields)] TTuple>(
        bool write,
        [NotNullWhen(true)] out CsvBindingCollection<TTuple>? bindingCollection
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        if (!IsTuple(typeof(TTuple)))
        {
            bindingCollection = null;
            return false;
        }

        List<CsvBinding<TTuple>> bindingsList;

        if (write)
        {
            var fields = typeof(TTuple).GetFields();
            fields.AsSpan().Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name)); // ensure order Item1, Item2 etc.

            bindingsList = new(fields.Length);

            for (int i = 0; i < fields.Length; i++)
            {
                bindingsList.Add(
                    fields[i].FieldType == typeof(CsvIgnored)
                        ? new IgnoredCsvBinding<TTuple>(index: i)
                        : new MemberCsvBinding<TTuple>(index: i, (MemberData)fields[i])
                );
            }
        }
        else
        {
            var parameters = typeof(TTuple).GetConstructors()[0].GetParameters();
            bindingsList = new(parameters.Length);

            foreach (var parameter in parameters)
            {
                bindingsList.Add(
                    parameter.ParameterType == typeof(CsvIgnored)
                        ? new IgnoredCsvBinding<TTuple>(index: parameter.Position)
                        : new ParameterCsvBinding<TTuple>(index: parameter.Position, parameter)
                );
            }
        }

        bindingCollection = new CsvBindingCollection<TTuple>(bindingsList, write);
        return true;
    }
}
