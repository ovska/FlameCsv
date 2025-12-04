using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;

namespace FlameCsv.Reading;

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
        CsvOptions<T> options
    )
    {
        if (binding.IsIgnored)
        {
            return CsvIgnored.Converter<T, TConverted>();
        }

        return binding.ResolveConverter<T, TConverted>(options) ?? options.GetConverter<TConverted>();
    }

    protected virtual (Type type, object converter) GetExceptionMetadata(int index) => (typeof(void), new object());

    [MethodImpl(MethodImplOptions.NoInlining)]
    [DoesNotReturn]
    protected TValue ThrowParseException(int index)
    {
        string name = _bindings.Bindings[index].DisplayName;
        (Type type, object converter) = GetExceptionMetadata(index);
        CsvParseException.Throw(index, type, converter, name);
        return default; // unreachable
    }
}
