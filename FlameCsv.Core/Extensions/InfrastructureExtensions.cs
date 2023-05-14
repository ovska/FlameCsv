using System.Diagnostics;
using CommunityToolkit.Diagnostics;

namespace FlameCsv.Extensions;

internal static class InfrastructureExtensions
{
    public static CsvConverter<T> GetParserOrFromFactory<T>(
        this CsvConverter<T> parserOrFactory,
        Type targetType,
        CsvOptions<T> readerOptions)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(parserOrFactory.CanConvert(targetType));

        if (parserOrFactory is not CsvConverterFactory<T> factory)
        {
            return parserOrFactory;
        }

        if (targetType.IsGenericTypeDefinition)
            throw new ArgumentException($"Cannot create a parser for generic type {targetType.ToTypeString()}");

        CsvConverter<T> createdParser = factory.Create(targetType, readerOptions)
            ?? throw new InvalidOperationException(
                $"Factory {factory.GetType().ToTypeString()} returned null " +
                $"when creating parser for type {targetType.ToTypeString()}");

        Debug.Assert(createdParser.CanConvert(targetType));
        return createdParser;
    }
}
