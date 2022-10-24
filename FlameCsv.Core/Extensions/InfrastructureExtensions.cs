using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Parsers;

namespace FlameCsv.Extensions;

internal static class InfrastructureExtensions
{
    public static ICsvParser<T> GetParserOrFromFactory<T>(
        this ICsvParser<T> parserOrFactory,
        Type targetType,
        CsvConfiguration<T> configuration)
        where T : unmanaged, IEquatable<T>
    {
        return parserOrFactory is ICsvParserFactory<T> factory
            ? factory.CreateAndThrowIfNull(targetType, configuration)
            : parserOrFactory;
    }

    public static ICsvParser<T> CreateAndThrowIfNull<T>(
        this ICsvParserFactory<T> factory,
        Type targetType,
        CsvConfiguration<T> configuration)
        where T : unmanaged, IEquatable<T>
    {
        return factory.Create(targetType, configuration)
            ?? throw new CsvConfigurationException(
                $"Factory {factory.GetType().ToTypeString()} returned null when "
                + $"creating parser for type {targetType.ToTypeString()}");
    }
}
