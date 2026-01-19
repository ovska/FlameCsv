using FlameCsv.Binding;
using FlameCsv.Extensions;

namespace FlameCsv.Writing;

[System.Runtime.CompilerServices.SkipLocalsInit]
internal abstract class Dematerializer<T, TValue> : Dematerializer<T>, IDematerializer<T, TValue>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvBindingCollection<TValue> _bindings;

    public abstract int FieldCount { get; }
    public abstract void Write(CsvFieldWriter<T> writer, TValue value);

    protected Dematerializer(CsvBindingCollection<TValue> bindings)
    {
        _bindings = bindings;
    }

    public void WriteHeader(CsvFieldWriter<T> writer)
    {
        ReadOnlySpan<MemberCsvBinding<TValue>> bindings = _bindings.MemberBindings;

        for (int i = 0; i < bindings.Length; i++)
        {
            if (i != 0)
                writer.WriteDelimiter();

            if (bindings[i].Header is null)
                Throw.InvalidOp_NoHeader(i, typeof(TValue), bindings[i].Member);

            writer.WriteText(bindings[i].Header);
        }
    }

    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    protected static CsvConverter<T, TConverted> ResolveConverter<TConverted>(
        CsvOptions<T> options,
        MemberCsvBinding<TValue> binding
    )
    {
        if (binding.IsIgnored)
        {
            return CsvIgnored.Converter<T, TConverted>();
        }

        return binding.ResolveConverter<T, TConverted>(options) ?? options.GetConverter<TConverted>();
    }
}
