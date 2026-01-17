using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv;

internal readonly struct ConverterBuilder<T> : IWrapper<CsvConverter<T>, ConverterBuilder<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly Lazy<CsvConverter<T>> _value = new();
    private readonly Func<Type, ConverterBuilder<T>, bool> _canConvert;

    public ConverterBuilder(CsvConverter<T> value)
    {
        _value = new Lazy<CsvConverter<T>>(value);
        _canConvert = static (type, b) => b._value.Value.CanConvert(type);
    }

    public ConverterBuilder(
        CsvOptions<T> options,
        Func<CsvOptions<T>, CsvConverter<T>> value,
        Func<Type, ConverterBuilder<T>, bool> canConvert
    )
    {
        _value = new Lazy<CsvConverter<T>>(() => value(options));
        _canConvert = canConvert;
    }

    public bool CanConvert(Type type) => _canConvert(type, this);

    public static ConverterBuilder<T> Wrap(CsvConverter<T> value) => new(value);

    public CsvConverter<T> Unwrap() => _value.Value;

    public static CsvConverter<T> Unwrap(ConverterBuilder<T> value) => value._value.Value;
}

/// <summary>
/// Base class used for registering custom converters.<br/>
/// Implement a converter
/// by inheriting either <see cref="CsvConverter{T, TValue}"/> or <see cref="CsvConverterFactory{T}"/>
/// </summary>
/// <remarks>
/// Converter instances must be safe to use concurrently.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public abstract class CsvConverter<T>
    where T : unmanaged, IBinaryInteger<T>
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
    private protected CsvConverter() { }

    /// <summary>
    /// Returns the converter instance, of creates a new one if the current instance is a factory.
    /// </summary>
    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    internal CsvConverter<T> GetAsConverter(Type targetType, CsvOptions<T> options)
    {
        // note: this cannot be made abstract and overridden in factory and converter classes
        // as the trimming annotations need to be consistent, but are only needed for factories

        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(options);

        if (!CanConvert(targetType))
        {
            Throw.InvalidOperation($"Converter {GetType().FullName} cannot convert type {targetType.FullName}");
        }

        if (this is CsvConverterFactory<T> factory)
        {
            CsvConverter<T> created = factory.Create(targetType, options);

            if (created is null)
            {
                Throw.InvalidOperation(
                    $"Factory {GetType().FullName} returned null when creating converter for type {targetType.FullName}"
                );
            }

            if (created is CsvConverterFactory<T>)
            {
                Throw.InvalidOperation(
                    $"Factory {GetType().FullName} returned another factory ({created.GetType().FullName}) when creating converter for type {targetType.FullName}"
                );
            }

            if (!created.CanConvert(targetType))
            {
                Throw.InvalidOperation(
                    $"Factory {GetType().FullName} returned converter ({created.GetType().FullName}) that cannot convert type {targetType.FullName}"
                );
            }

            return created;
        }

        return this;
    }
}
