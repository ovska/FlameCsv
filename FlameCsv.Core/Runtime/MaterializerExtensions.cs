using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Reflection;

namespace FlameCsv.Runtime;

internal static class MaterializerExtensions
{
    private static class ForType<T, TResult> where T : unmanaged, IEquatable<T>
    {
        /// <summary>
        /// Generator instance to create delegates through reflection.
        /// </summary>
        public static readonly DelegateGenerator<T> Generator = new ExpressionDelegateGenerator<T>();

        /// <summary>
        /// Cached bindings for headerless CSV.
        /// </summary>
        public static DelegateGenerator<T>.MaterializerFactory<TResult>? Cached;
    }

    /// <summary>
    /// Creates a materializer from the bindings.
    /// </summary>
    public static IMaterializer<T, TResult> CreateMaterializerFrom<T, TResult>(
        this CsvReaderOptions<T> options,
        CsvBindingCollection<TResult> bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        return ForType<T, TResult>.Generator.GetMaterializerFactory(bindingCollection)(options);
    }

    /// <summary>
    /// Binds the options using built-in or index binding.
    /// </summary>
    public static IMaterializer<T, TResult> GetMaterializer<T, TResult>(this CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        DelegateGenerator<T>.MaterializerFactory<TResult>? factory = ForType<T, TResult>.Cached;

        if (factory is null)
        {
            if (TryGetTupleBindings<T, TResult>(out var bindings) ||
                IndexAttributeBinder<TResult>.TryGetBindings(out bindings))
            {
                factory = ForType<T, TResult>.Generator.GetMaterializerFactory(bindings);
            }
            else
            {
                // Don't cache nulls since its unlikely they will be attempted many times
                throw new CsvBindingException<TResult>(
                    $"Headerless CSV could not be bound to {typeof(TResult)}, since the type had no " +
                    "[CsvIndex]-attributes and no built-in configuration.");
            }

            factory =
                Interlocked.CompareExchange(ref ForType<T, TResult>.Cached, factory, null)
                ?? factory;
        }

        return factory(options);
    }

    private static bool TryGetTupleBindings<T, TTuple>([NotNullWhen(true)] out CsvBindingCollection<TTuple>? bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        if (!ReflectionUtil.IsTuple<TTuple>())
        {
            bindingCollection = null;
            return false;
        }

        var parameters = typeof(TTuple).GetConstructors()[0].GetParameters();
        var bindingsList = new List<CsvBinding<TTuple>>(parameters.Length);

        // TODO: add support for ignored fields via a special type, e.g. struct CsvIgnore { }
        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            bindingsList.Add(new ParameterCsvBinding<TTuple>(index: parameter.Position, parameter));
        }

        bindingCollection = new CsvBindingCollection<TTuple>(bindingsList, isInternalCall: true);
        return true;
    }
}
