using System.Reflection;
using FlameCsv.Exceptions;
using FlameCsv.Reflection;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the converter for the target member or parameter.
/// </summary>
/// <remarks>
/// Converters created this way are cached in <see cref="CsvOptions{T}"/> per member/parameter.
/// </remarks>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TConverter"></typeparam>
public sealed class CsvConverterAttribute<T, [DAM(Messages.Ctors)] TConverter>
    : CsvConverterAttribute<T>
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
        bool isInherited = options.GetType() != typeof(CsvOptions<T>);

        ConstructorInfo? inherited = null;
        ConstructorInfo? notInherited = null;

        foreach ((ConstructorInfo ctor, ParameterData[] parameters) in CsvTypeInfo.PublicConstructors<TConverter>())
        {
            if (parameters.Length == 1)
            {
                if (isInherited && options.GetType() == parameters[0].Value.ParameterType)
                {
                    inherited = ctor;
                }
                else if (parameters[0].Value.ParameterType == typeof(CsvOptions<T>))
                {
                    notInherited = ctor;
                }
            }
        }

        object[]? args = null;

        if ((inherited ?? notInherited) is { } valid)
        {
            args = [options];
        }
        else if ((valid = CsvTypeInfo.EmptyConstructor<TConverter>()) is not null)
        {
            args = [];
        }

        if (args is null || valid is null)
        {
            string suffix = isInherited ? $" or {options.GetType().FullName}" : "";

            throw new CsvConfigurationException(
                $"{typeof(TConverter).FullName} has constructor that accepts {typeof(CsvOptions<T>).FullName}{suffix} or no parameters");
        }

        return (CsvConverter<T>)valid.Invoke(args);
    }
}
