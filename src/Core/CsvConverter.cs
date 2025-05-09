using System.Diagnostics;
using FlameCsv.Extensions;

namespace FlameCsv;

/// <summary>
/// Base class used for registering custom converters.<br/>
/// Implement a converter
/// by inheriting either <see cref="CsvConverter{T, TValue}"/> or <see cref="CsvConverterFactory{T}"/>
/// </summary>
/// <remarks>
/// Converter instances must be safe to use concurrently.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public abstract class CsvConverter<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns whether the type can be handled by this converter, or a suitable converter can be
    /// created from this factory instance.
    /// </summary>
    /// <param name="type">Type to check</param>
    /// <returns><c>true</c> if the converter is suitable for <paramref name="type"/></returns>
    public abstract bool CanConvert(Type type);

    /// <summary>
    /// Constructor to prevent direct inheritance.
    /// </summary>
    private protected CsvConverter()
    {
    }

    /// <summary>
    /// Returns the converter instance, of creates a new one if the current instance is a factory.
    /// </summary>
    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    internal CsvConverter<T> GetAsConverter(Type targetType, CsvOptions<T> readerOptions)
    {
        // note: this cannot be made abstract and overridden in factory and converter classes
        // as the trimming annotations need to be consistent, but are only needed for factories

        Debug.Assert(targetType is not null);
        Debug.Assert(readerOptions is not null);
        Debug.Assert(CanConvert(targetType));

        if (this is CsvConverterFactory<T> factory)
        {
            CsvConverter<T> created = factory.Create(targetType, readerOptions);

            if (created is null)
            {
                Throw.InvalidOperation(
                    $"Factory {GetType().FullName} returned null when creating converter for type {targetType.FullName}");
            }

            if (created is CsvConverterFactory<T>)
            {
                Throw.InvalidOperation(
                    $"Factory {GetType().FullName} returned another factory ({created.GetType().FullName}) when creating converter for type {targetType.FullName}");
            }

            return created;
        }

        return this;
    }
}
