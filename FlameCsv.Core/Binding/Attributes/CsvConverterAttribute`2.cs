using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the converter for the target member or parameter.
/// </summary>
/// <remarks>
/// Converter created this way are not cached in <see cref="CsvOptions{T}"/>,
/// and a new instance is created for every overridden property if necessary.
/// </remarks>
/// <typeparam name="T"></typeparam>
/// <typeparam name="TConverter"></typeparam>
public sealed class CsvConverterAttribute<T, [DynamicallyAccessedMembers(Messages.Ctors)] TConverter> : CsvConverterAttribute<T>
    where T : unmanaged, IEquatable<T>
    where TConverter : CsvConverter<T>
{
    /// <inheritdoc cref="CsvConverterAttribute{T, TParser}"/>
    public CsvConverterAttribute()
    {
    }

    protected override CsvConverter<T> CreateConverterOrFactory(Type targetType, CsvOptions<T> options)
    {
        if (options.GetType() != typeof(CsvOptions<T>) &&
            typeof(TConverter).GetConstructor([options.GetType()]) is { } exactCtor)
        {
            return (CsvConverter<T>)exactCtor.Invoke(new object[] { options });
        }

        if (typeof(TConverter).GetConstructor([typeof(CsvOptions<T>)]) is { } baseTypeCtor)
        {
            return (CsvConverter<T>)baseTypeCtor.Invoke(new object[] { options });
        }

        if (typeof(TConverter).GetConstructor(Type.EmptyTypes) is { } emptyCtor)
        {
            return (CsvConverter<T>)emptyCtor.Invoke([]);
        }

        throw new CsvConfigurationException(
            $"Parser type {typeof(TConverter).ToTypeString()} has no valid constructor!");
    }
}
