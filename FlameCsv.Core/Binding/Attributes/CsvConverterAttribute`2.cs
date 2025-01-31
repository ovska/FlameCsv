using System.Reflection;
using FlameCsv.Exceptions;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the converter for the target member or parameter.
/// This attribute is recognized by the source generator.
/// </summary>
/// <remarks>
/// Converters created this way are distinct from the cached converters in <see cref="CsvOptions{T}"/>.
/// </remarks>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TConverter"></typeparam>
public sealed class CsvConverterAttribute<T, [DAM(Messages.Ctors)] TConverter> : CsvConverterAttribute<T>
    where T : unmanaged, IBinaryInteger<T>
    where TConverter : CsvConverter<T>
{
    private static readonly Lazy<(ConstructorInfo ctor, bool empty)> _converterConstructor = new(
        static () =>
        {
            ConstructorInfo? empty = null;

            foreach (var ctor in typeof(TConverter).GetConstructors())
            {
                var parameters = ctor.GetParameters();

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

            throw new CsvConfigurationException($"{typeof(TConverter).FullName} has no parameterless constructor");
        });

    /// <inheritdoc cref="CsvConverterAttribute{T, TParser}"/>
    public CsvConverterAttribute()
    {
    }

    /// <inheritdoc/>
    protected override CsvConverter<T> CreateConverterOrFactory(Type targetType, CsvOptions<T> options)
    {
        (ConstructorInfo ctor, bool empty) = _converterConstructor.Value;
        return (CsvConverter<T>)ctor.Invoke(empty ? [] : [options]);
    }
}
