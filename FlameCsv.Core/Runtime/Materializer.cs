using FlameCsv.Binding;

namespace FlameCsv.Runtime;

internal abstract class Materializer<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvBindingCollection<TValue> _bindings;

    protected Materializer(CsvBindingCollection<TValue> bindings)
    {
        _bindings = bindings;
    }

    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    protected static CsvConverter<T, TConverted> ResolveConverter<TConverted>(
        CsvBinding<TValue> binding,
        CsvOptions<T> options)
    {
        if (binding.IsIgnored)
        {
            return CsvIgnored.Converter<T, TConverted>();
        }

        return binding.ResolveConverter<T, TConverted>(options) ?? options.GetConverter<TConverted>();
    }

    protected string GetName(int index) => _bindings.Bindings[index].DisplayName;
}
