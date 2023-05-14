using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Converters;
using FlameCsv.Reading;

namespace FlameCsv.Runtime;

[RequiresUnreferencedCode(Messages.CompiledExpressions)]
internal abstract class DelegateGenerator<T> where T : unmanaged, IEquatable<T>
{
    public delegate IMaterializer<T, TResult> MaterializerFactory<TResult>(CsvOptions<T> options);

    protected abstract Func<object[], IMaterializer<T, TResult>> GetMaterializerInit<[DynamicallyAccessedMembers(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc);

    protected abstract Delegate GetValueFactory<[DynamicallyAccessedMembers(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc);

    public MaterializerFactory<TResult> GetMaterializerFactory<[DynamicallyAccessedMembers(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc)
    {
        ArgumentNullException.ThrowIfNull(bc);
        return new CreateMaterializerValue<TResult>
        {
            BindingCollection = bc,
            ValueFactory = GetValueFactory(bc),
            MaterializerFactory = GetMaterializerInit(bc)
        }.Invoke;
    }

    private readonly struct CreateMaterializerValue<TResult>
    {
        public CsvBindingCollection<TResult> BindingCollection { get; init; }
        public Delegate ValueFactory { get; init; }
        public Func<object[], IMaterializer<T, TResult>> MaterializerFactory { get; init; }

        public IMaterializer<T, TResult> Invoke(CsvOptions<T> options)
        {
            var bindings = BindingCollection.Bindings;

            object[] materializerCtorArgs = new object[bindings.Length + 1];
            materializerCtorArgs[0] = ValueFactory;

            for (int i = 0; i < bindings.Length; i++)
            {
                materializerCtorArgs[i + 1] = ResolveConverter(bindings[i], options);
            }

            return MaterializerFactory(materializerCtorArgs);
        }

        private static CsvConverter<T> ResolveConverter(CsvBinding<TResult> binding, CsvOptions<T> options)
        {
            if (binding.IsIgnored)
            {
                return IgnoredConverter<T>.Instance;
            }

            if (binding.TryGetAttribute<CsvConverterAttribute<T>>(out var @override))
            {
                return @override.CreateParser(binding.Type, options);
            }

            return options.GetConverter(binding.Type);
        }
    }
}
