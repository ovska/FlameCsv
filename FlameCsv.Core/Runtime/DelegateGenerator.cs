using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Parsers;
using FlameCsv.Reading;

namespace FlameCsv.Runtime;

[RequiresUnreferencedCode(Messages.CompiledExpressions)]
internal abstract class DelegateGenerator<T> where T : unmanaged, IEquatable<T>
{
    public delegate IMaterializer<T, TResult> MaterializerFactory<TResult>(CsvReaderOptions<T> options);

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

        public IMaterializer<T, TResult> Invoke(CsvReaderOptions<T> options)
        {
            var bindings = BindingCollection.Bindings;

            object[] materializerCtorArgs = new object[bindings.Length + 1];
            materializerCtorArgs[0] = ValueFactory;

            for (int i = 0; i < bindings.Length; i++)
            {
                materializerCtorArgs[i + 1] = ResolveParser(bindings[i], options);
            }

            return MaterializerFactory(materializerCtorArgs);
        }

        private static ICsvParser<T> ResolveParser(CsvBinding<TResult> binding, CsvReaderOptions<T> options)
        {
            if (binding.IsIgnored)
            {
                return IgnoredFieldParser<T>.Instance;
            }

            if (binding.TryGetAttribute<CsvParserOverrideAttribute>(out var @override))
            {
                return @override.CreateParser(binding.Type, options);
            }

            return options.GetParser(binding.Type);
        }
    }
}
