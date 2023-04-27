using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using FlameCsv.Parsers;

namespace FlameCsv.Extensions;

internal static class InfrastructureExtensions
{
    public static ICsvParser<T> GetParserOrFromFactory<T>(
        this ICsvParser<T> parserOrFactory,
        Type targetType,
        CsvReaderOptions<T> readerOptions)
        where T : unmanaged, IEquatable<T>
    {
        Debug.Assert(parserOrFactory.CanParse(targetType));

        if (parserOrFactory is not ICsvParserFactory<T> factory)
        {
            return parserOrFactory;
        }

        if (targetType.IsGenericTypeDefinition)
            throw new ArgumentException($"Cannot create a parser for generic type {targetType.ToTypeString()}");

        ICsvParser<T> createdParser = factory.Create(targetType, readerOptions)
            ?? throw new InvalidOperationException(
                $"Factory {factory.GetType().ToTypeString()} returned null " +
                $"when creating parser for type {targetType.ToTypeString()}");

        Debug.Assert(createdParser.CanParse(targetType));
        return createdParser;
    }
}
