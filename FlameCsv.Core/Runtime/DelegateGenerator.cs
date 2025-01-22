using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Reading;

namespace FlameCsv.Runtime;

internal abstract class DelegateGenerator<T> where T : unmanaged, IBinaryInteger<T>
{
    public delegate IMaterializer<T, TResult> MaterializerFactory<out TResult>(CsvOptions<T> options);

    protected abstract Func<object[], IMaterializer<T, TResult>> GetMaterializerInit<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc);

    protected abstract Delegate GetValueFactory<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc);

    public MaterializerFactory<TResult> GetMaterializerFactory<[DAM(Messages.Ctors)] TResult>(
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
                return CsvIgnored.Converter<T>();
            }

            foreach (var attribute in binding.Attributes)
            {
                if (attribute is CsvConverterAttribute<T> @override && @override.Scope != CsvBindingScope.Write)
                    return @override.CreateConverter(binding.Type, options);
            }

            return options.GetConverter(binding.Type);
        }
    }
}
