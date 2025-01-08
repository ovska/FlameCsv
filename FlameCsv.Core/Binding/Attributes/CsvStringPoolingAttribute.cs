using System.Runtime.CompilerServices;
using FlameCsv.Converters;

namespace FlameCsv.Binding.Attributes;

public sealed class CsvStringPoolingAttribute<T> : CsvConverterAttribute<T> where T : unmanaged, IBinaryInteger<T>
{
    protected override CsvConverter<T> CreateConverterOrFactory(Type targetType, CsvOptions<T> options)
    {
        if (targetType != typeof(string))
        {
            throw new InvalidOperationException($"{GetType().FullName} must be applied on string, was: {targetType.FullName}");
        }

        if (typeof(T) == typeof(char))
        {
            return Unsafe.As<CsvConverter<T>>(PoolingStringTextConverter.SharedInstance);
        }

        if (typeof(T) == typeof(byte))
        {
            return Unsafe.As<CsvConverter<T>>(PoolingStringUtf8Converter.SharedInstance);
        }

        throw new NotSupportedException($"{GetType().FullName} does not support token type {typeof(T).FullName}");
    }
}
