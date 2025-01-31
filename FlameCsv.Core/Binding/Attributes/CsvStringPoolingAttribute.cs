using CommunityToolkit.HighPerformance.Buffers;
using FlameCsv.Converters;
#if DEBUG
using Unsafe = FlameCsv.Extensions.DebugUnsafe
#else
using Unsafe = System.Runtime.CompilerServices.Unsafe
#endif
    ;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Configures the member to use pooled strings.
/// </summary>
/// <seealso cref="CsvOptions{T}.StringPool"/>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CsvStringPoolingAttribute<T> : CsvConverterAttribute<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc />
    protected override CsvConverter<T> CreateConverterOrFactory(Type targetType, CsvOptions<T> options)
    {
        if (targetType != typeof(string))
        {
            throw new InvalidOperationException(
                $"{GetType().FullName} must be applied on string, was: {targetType.FullName}");
        }

        StringPool? configured = options.StringPool;

        if (typeof(T) == typeof(char))
        {
            CsvConverter<char> converter = configured is not null && configured != StringPool.Shared
                ? new PoolingStringTextConverter(configured)
                : PoolingStringTextConverter.SharedInstance;
            return Unsafe.As<CsvConverter<T>>(converter);
        }

        if (typeof(T) == typeof(byte))
        {
            CsvConverter<byte> converter = configured is not null && configured != StringPool.Shared
                ? new PoolingStringUtf8Converter(configured)
                : PoolingStringUtf8Converter.SharedInstance;
            return Unsafe.As<CsvConverter<T>>(converter);
        }

        throw new NotSupportedException($"{GetType().FullName} does not support token type {typeof(T).FullName}");
    }
}
