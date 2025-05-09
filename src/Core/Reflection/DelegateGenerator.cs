using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.Reflection;

internal abstract class DelegateGenerator<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public delegate IMaterializer<T, TResult> MaterializerFactory<out TResult>(CsvOptions<T> options);

    protected abstract Func<object[], IMaterializer<T, TResult>> GetMaterializerInit<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc
    );

    protected abstract Delegate GetValueFactory<[DAM(Messages.Ctors)] TResult>(CsvBindingCollection<TResult> bc);

    public MaterializerFactory<TResult> GetMaterializerFactory<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc
    )
    {
        ArgumentNullException.ThrowIfNull(bc);
        var valueFactory = GetValueFactory(bc);
        var materializerFactory = GetMaterializerInit(bc);
        return options => materializerFactory.Invoke([valueFactory, bc, options]);
    }
}
