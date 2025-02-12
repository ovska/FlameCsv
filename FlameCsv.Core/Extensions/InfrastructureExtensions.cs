using System.Diagnostics;

namespace FlameCsv.Extensions;

internal static class InfrastructureExtensions
{
    [RUF(Messages.FactoryMethod), RDC(Messages.FactoryMethod)]
    public static CsvConverter<T> GetOrCreateConverter<T>(
        this CsvConverter<T> parserOrFactory,
        Type targetType,
        CsvOptions<T> readerOptions)
        where T : unmanaged, IBinaryInteger<T>
    {
        Debug.Assert(parserOrFactory.CanConvert(targetType));

        if (parserOrFactory is not CsvConverterFactory<T> factory)
        {
            return parserOrFactory;
        }

        if (targetType.IsGenericTypeDefinition)
            throw new ArgumentException($"Cannot create a parser for generic type {targetType.FullName}");

        CsvConverter<T> createdParser = factory.Create(targetType, readerOptions)
            ?? throw new InvalidOperationException(
                $"Factory {factory.GetType().FullName} returned null when creating parser for type {targetType.FullName}");

        Debug.Assert(
            createdParser.CanConvert(targetType) && createdParser is not CsvConverterFactory<T>,
            $"Invalid factory: {createdParser.GetType()}");
        return createdParser;
    }
}
