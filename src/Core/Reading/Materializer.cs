using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Intrinsics;
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
        if (typeof(TConverted) == typeof(CsvIgnored))
        {
            Check.True(true, $"Invalid ignored binding: {binding}");
            return CsvIgnored.Converter<T, TConverted>();
        }

        Check.False(binding.IsIgnored, $"Binding is ignored: {binding}");
        return binding.ResolveConverter<T, TConverted>(options) ?? options.GetConverter<TConverted>();
    }

    protected static int[]? GetIgnoredIndexes(scoped ReadOnlySpan<CsvBinding<TValue>> bindings, CsvOptions<T> options)
    {
        if (options.Quote is null || options.ValidateQuotes < CsvQuoteValidation.ValidateUnreadFields)
        {
            return null;
        }

        using ValueListBuilder<int> list = new(stackalloc int[4]);

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
    protected TValue ThrowParseException(uint flags)
    {
        Check.NotEqual(flags, 0u, "No parse failures to report?");

        int index = BitOperations.TrailingZeroCount(flags);

        string name = _bindings.Bindings[index].DisplayName;
        (Type type, object converter) = GetExceptionMetadata(index);

        var vsb = new ValueStringBuilder();
        vsb.Append("Failed to parse ");
        vsb.Append(type.Name);
        vsb.Append(' ');
        vsb.Append(name);
        vsb.Append(" using ");
        vsb.Append(converter.GetType().Name);

        if ((flags = Bithacks.ResetLowestSetBit(flags)) != 0)
        {
            vsb.Append(", (additional unparsable fields:");

            do
            {
                vsb.Append(' ');
                vsb.AppendFormatted(BitOperations.TrailingZeroCount(flags));
                vsb.Append(',');
                flags = Bithacks.ResetLowestSetBit(flags);
            } while (flags != 0);

            vsb.Length--;
            vsb.Append(')');
        }

        vsb.Append('.');

        throw new CsvParseException(vsb.ToString())
        {
            Converter = converter,
            FieldIndex = index,
            Target = name,
            TargetType = type,
        };
    }
}
