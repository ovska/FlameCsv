using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlameCsv.Exceptions;

namespace FlameCsv.Attributes;

/// <summary>
/// Overrides the converter for the target member or parameter.<br/>
/// <typeparamref name="TConverter"/> must have a parameterless constructor,
/// or a public constructor with a single <see cref="CsvOptions{T}"/> parameter.
/// </summary>
/// <remarks>
/// Converters created this way are distinct from the cached converters in <see cref="CsvOptions{T}"/>.<br/>
/// The resulting converter is cast to <see cref="CsvConverter{T,TValue}"/>.
/// </remarks>
/// <typeparam name="TConverter">Converter or factory type</typeparam>
public sealed class CsvConverterAttribute<[DAM(Messages.Ctors)] TConverter> : CsvConverterAttribute
    where TConverter : class
{
    /// <inheritdoc/>
    protected override bool TryCreateConverterOrFactory<T>(
        Type targetType,
        CsvOptions<T> options,
        [NotNullWhen(true)] out CsvConverter<T>? converter
    )
    {
        if (!typeof(TConverter).IsAssignableTo(typeof(CsvConverter<T>)))
        {
            converter = null;
            return false;
        }

        (ConstructorInfo ctor, bool empty) = Ctor<T>.Value.Value;
        converter = (CsvConverter<T>)ctor.Invoke(empty ? [] : [options]);
        return true;
    }

    private static class Ctor<T>
        where T : unmanaged, IBinaryInteger<T>
    {
        public static readonly Lazy<(ConstructorInfo ctor, bool empty)> Value = new(static () =>
        {
            ConstructorInfo? empty = null;

            foreach (var ctor in typeof(TConverter).GetConstructors())
            {
                ParameterInfo[] parameters = ctor.GetParameters();

                if (parameters.Length == 0)
                {
                    empty ??= ctor;
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(CsvOptions<T>))
                {
                    return (ctor, false);
                }
            }

            if (empty is not null)
            {
                return (empty, true);
            }

            throw new CsvConfigurationException(
                $"{typeof(TConverter).FullName} has no parameterless constructor "
                    + $"or constructor with a single {typeof(CsvOptions<T>).FullName} parameter."
            );
        });
    }
}
