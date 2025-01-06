using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Reading;
using FlameCsv.Reflection;
using DAMT = System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes;

namespace FlameCsv.Runtime;

internal static class MaterializerExtensions
{
    [RUF(Messages.CompiledExpressions)]
    [RDC(Messages.CompiledExpressions)]
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
    [RUF(Messages.CompiledExpressions)]
    [RDC(Messages.CompiledExpressions)]
    public static IMaterializer<T, TResult> CreateMaterializerFrom<T, [DAM(Messages.ReflectionBound)] TResult>(
        this CsvOptions<T> options,
        CsvBindingCollection<TResult> bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        return ForType<T, TResult>.Generator.GetMaterializerFactory(bindingCollection)(options);
    }

    /// <summary>
    /// Binds the options using built-in or index binding.
    /// </summary>
    [RUF(Messages.CompiledExpressions)]
    [RDC(Messages.CompiledExpressions)]
    public static IMaterializer<T, TResult> GetMaterializer<T, [DAM(Messages.ReflectionBound)] TResult>(
        this CsvOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        DelegateGenerator<T>.MaterializerFactory<TResult>? factory = ForType<T, TResult>.Cached;

        if (factory is null)
        {
            if (TryGetTupleBindings<T, TResult>(write: false, out var bindings)
                || IndexAttributeBinder<TResult>.TryGetBindings(write: false, out bindings))
            {
                factory = ForType<T, TResult>.Generator.GetMaterializerFactory(bindings);
            }
            else
            {
                // Don't cache nulls since its unlikely they will be attempted many times
                throw new CsvBindingException<TResult>(
                    $"Headerless CSV could not be bound to {typeof(TResult)}, since the type had no "
                    + "[CsvIndex]-attributes and no built-in configuration.");
            }

            factory = Interlocked.CompareExchange(ref ForType<T, TResult>.Cached, factory, null)
                ?? factory;
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

    private static bool TryGetTupleBindings<T, [DAM(DAMT.PublicConstructors | DAMT.PublicFields)] TTuple>(
        bool write,
        [NotNullWhen(true)] out CsvBindingCollection<TTuple>? bindingCollection)
        where T : unmanaged, IEquatable<T>
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
            fields.AsSpan()
                .Sort(
                    static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name)); // ensure order Item1, Item2 etc.

            bindingsList = new(fields.Length);

            for (int i = 0; i < fields.Length; i++)
            {
                bindingsList.Add(
                    fields[i].FieldType == typeof(CsvIgnored)
                        ? new IgnoredCsvBinding<TTuple>(index: i)
                        : new MemberCsvBinding<TTuple>(index: i, (MemberData)fields[i]));
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
                        : new ParameterCsvBinding<TTuple>(index: parameter.Position, parameter));
            }
        }

        bindingCollection = new CsvBindingCollection<TTuple>(bindingsList, write, isInternalCall: true);
        return true;
    }
}
