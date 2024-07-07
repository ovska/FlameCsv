using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the converter for the target member or parameter.
/// </summary>
/// <remarks>
/// Converters created this way are cached in <see cref="CsvOptions{T}"/> per member/parameter.
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
        bool isInherited = options.GetType() != typeof(CsvOptions<T>);

        // check for possible inherited ctor
        if (isInherited && typeof(TConverter).GetConstructor([options.GetType()]) is { } exactCtor)
        {
            return (CsvConverter<T>)exactCtor.Invoke([options]);
        }

        if (typeof(TConverter).GetConstructor([typeof(CsvOptions<T>)]) is { } baseTypeCtor)
        {
            return (CsvConverter<T>)baseTypeCtor.Invoke([options]);
        }

        if (typeof(TConverter).GetConstructor(Type.EmptyTypes) is { } emptyCtor)
        {
            return (CsvConverter<T>)emptyCtor.Invoke([]);
        }

        string suffix = isInherited ? $" or {options.GetType().ToTypeString()}" : "";

        throw new CsvConfigurationException(
            $"{typeof(TConverter).ToTypeString()} has constructor that accepts {typeof(CsvOptions<T>).ToTypeString()}{suffix} or no parameters");
    }
}
