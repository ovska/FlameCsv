using CommunityToolkit.Diagnostics;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents an error of a parser for the specified type being missing in the configuration.
/// </summary>
public sealed class CsvParserMissingException : CsvConfigurationException
{
    /// <summary>
    /// Parsed token type, such as <see cref="Char"/> or <see cref="Byte"/>.
    /// </summary>
    public Type TokenType { get; }

    /// <summary>
    /// Type the parser is for.
    /// </summary>
    public Type ResultType { get; }

    /// <summary>
    /// Initializes an exception representing a missing parser for the specified type.
    /// </summary>
    /// <param name="tokenType">Type parameter of the <see cref="CsvOptions{T}"/></param>
    /// <param name="resultType">Type the parser was requested for</param>
    public CsvParserMissingException(Type tokenType, Type resultType)
        : base($"Parser not found for type: {resultType.ToTypeString()}")
    {
        TokenType = tokenType;
        ResultType = resultType;
    }
}
