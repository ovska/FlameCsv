using System.Diagnostics;
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
public sealed class CsvConverterMissingException(Type resultType, Exception? innerException = null)
    : CsvConfigurationException($"Converter not found for type {resultType.FullName}", innerException)
{
    /// <summary>
    /// Type the converter is for.
    /// </summary>
    public Type ResultType { get; } = resultType;

    /// <exception cref="CsvConverterMissingException" />
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [StackTraceHidden]
    public static void Throw(Type resultType)
    {
        throw new CsvConverterMissingException(resultType ?? typeof(void));
    }
}
