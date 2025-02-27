using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents an error of a converter for the specified type being missing in the configuration.
/// </summary>
/// <remarks>
/// Initializes an exception representing a missing converter for the specified type.
/// </remarks>
/// <param name="resultType">Type the parser was requested for</param>
[PublicAPI]
public sealed class CsvConverterMissingException(Type resultType)
    : CsvConfigurationException($"Converter not found for type {resultType.FullName}")
{
    /// <summary>
    /// Type the converter is for.
    /// </summary>
    public Type ResultType { get; } = resultType;

    /// <exception cref="CsvConverterMissingException">Always thrown</exception>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(Type resultType)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        throw new CsvConverterMissingException(resultType);
    }
}
