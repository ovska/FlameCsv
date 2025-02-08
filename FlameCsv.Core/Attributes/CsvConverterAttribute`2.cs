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
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TConverter">Converter or factory type</typeparam>
public sealed class CsvConverterAttribute<T, [DAM(Messages.Ctors)] TConverter> : CsvConverterAttribute<T>
    where T : unmanaged, IBinaryInteger<T>
    where TConverter : CsvConverter<T>
{
    /// <inheritdoc cref="CsvConverterAttribute{T, TParser}"/>
    public CsvConverterAttribute()
    {
    }

    /// <inheritdoc/>
    protected override CsvConverter<T> CreateConverterOrFactory(Type targetType, CsvOptions<T> options)
    {
        (ConstructorInfo ctor, bool empty) = _resolveConstructor.Value;
        return (CsvConverter<T>)ctor.Invoke(empty ? [] : [options]);
    }

    private static readonly Lazy<(ConstructorInfo ctor, bool empty)> _resolveConstructor = new(
        static () =>
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
                $"{typeof(TConverter).FullName} has no parameterless constructor " +
                $"or constructor with a single {typeof(CsvOptions<T>).FullName} parameter.");
        });
}
