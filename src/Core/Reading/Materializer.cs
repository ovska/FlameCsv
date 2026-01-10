using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;

namespace FlameCsv.Reading;

internal abstract class Materializer<T, TValue> : IMaterializer<T, TValue>
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
            Check.Equal(
                typeof(TConverted),
                typeof(CsvIgnored),
                $"Wrong converter type for ignored binding: {typeof(TConverted).FullName}"
            );

            return CsvIgnored.Converter<T, TConverted>();
        }

        return binding.ResolveConverter<T, TConverted>(options) ?? options.GetConverter<TConverted>();
    }

    protected static int[]? GetIgnoredIndexes(scoped ReadOnlySpan<CsvBinding<TValue>> bindings, CsvOptions<T> options)
    {
        if (options.Quote is null || options.ValidateQuotes < CsvQuoteValidation.ValidateUnreadFields)
        {
            return null;
        }

        using ValueListBuilder<int> list = new(stackalloc int[32]);

        foreach (var binding in bindings)
        {
            if (binding.IsIgnored)
            {
                list.Append(binding.Index);
            }
        }

        if (list.Length == 0)
        {
            return null;
        }

        return [.. list.AsSpan()];
    }

    public abstract TValue Parse(CsvRecordRef<T> record);

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
