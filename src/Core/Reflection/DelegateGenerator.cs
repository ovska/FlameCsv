using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.Reflection;

internal abstract class DelegateGenerator<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public delegate IMaterializer<T, TResult> MaterializerFactory<out TResult>(CsvOptions<T> options);

    protected abstract Func<object[], IMaterializer<T, TResult>> GetMaterializerInit<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc,
        out Type factoryType
    );

    protected abstract Delegate GetValueFactory<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc,
        Type factoryType
    );

    public MaterializerFactory<TResult> GetMaterializerFactory<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc
    )
    {
        ArgumentNullException.ThrowIfNull(bc);
        var materializerFactory = GetMaterializerInit(bc, out Type factoryType);
        var valueFactory = GetValueFactory(bc, factoryType);
        return options => materializerFactory.Invoke([valueFactory, bc, options]);
    }
}
