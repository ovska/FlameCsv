using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Configuration;
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
        if (parserOrFactory is not ICsvParserFactory<T> factory)
        {
            return parserOrFactory;
        }

        if (targetType.IsGenericTypeDefinition)
            throw new ArgumentException($"Cannot create a parser for generic type {targetType.ToTypeString()}");


        return factory.Create(targetType, readerOptions)
            ?? throw new InvalidOperationException(
                $"Factory {factory.GetType().ToTypeString()} returned null " +
                $"when creating parser for type {targetType.ToTypeString()}");
    }

    /// <summary>
    /// Returns a the null token for <paramref name="type"/>, the default null token for the config,
    /// or <see cref="ReadOnlyMemory{T}.Empty"/> if the config is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyMemory<T> GetNullTokenOrDefault<T>(
        this ICsvNullTokenConfiguration<T>? config,
        Type type)
        where T : unmanaged, IEquatable<T>
    {
        ReadOnlyMemory<T> value = default;

        if (config is not null && !config.TryGetOverride(type, out value))
        {
            value = config.Default;
        }

        return value;
    }
}
