using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Reading;
using FlameCsv.Reflection;

namespace FlameCsv.Runtime;

internal static class MaterializerExtensions
{
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
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
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static IMaterializer<T, TResult> CreateMaterializerFrom<T, [DynamicallyAccessedMembers(Messages.ReflectionBound)] TResult>(
        this CsvOptions<T> options,
        CsvBindingCollection<TResult> bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        return ForType<T, TResult>.Generator.GetMaterializerFactory(bindingCollection)(options);
    }

    /// <summary>
    /// Binds the options using built-in or index binding.
    /// </summary>
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static IMaterializer<T, TResult> GetMaterializer<T, [DynamicallyAccessedMembers(Messages.ReflectionBound)] TResult>(
        this CsvOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported, "Dynamic code is not supported");

        DelegateGenerator<T>.MaterializerFactory<TResult>? factory = ForType<T, TResult>.Cached;

        if (factory is null)
        {
            if (TryGetTupleBindings<T, TResult>(write: false, out var bindings) ||
                IndexAttributeBinder<TResult>.TryGetBindings(write: false, out bindings))
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

    private static bool TryGetTupleBindings<T,
        [DynamicallyAccessedMembers(Messages.Ctors)] TTuple>(
        bool write,
        [NotNullWhen(true)] out CsvBindingCollection<TTuple>? bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        if (!ReflectionUtil.IsTuple<TTuple>())
        {
            bindingCollection = null;
            return false;
        }

        var parameters = typeof(TTuple).GetConstructors()[0].GetParameters();
        var bindingsList = new List<CsvBinding<TTuple>>(parameters.Length);

        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            bindingsList.Add(
                parameter.GetType() == typeof(object)
                    ? new IgnoredCsvBinding<TTuple>(index: parameter.Position)
                    : new ParameterCsvBinding<TTuple>(index: parameter.Position, parameter));
        }

        bindingCollection = new CsvBindingCollection<TTuple>(bindingsList, write, isInternalCall: true);
        return true;
    }
}
