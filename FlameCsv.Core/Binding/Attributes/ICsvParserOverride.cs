using FlameCsv.Exceptions;
using FlameCsv.Parsers;

namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Overrides the default parser for the target member when implemented by an attribute.
/// A single attribute implementing this interface can be present on a member.
/// </summary>
public interface ICsvParserOverride
{
    /// <summary>
    /// Returns a parser for the binding's member.
    /// </summary>
    /// <exception cref="CsvConfigurationException">Thrown if the parser cannot be created</exception>
    ICsvParser<T> CreateParser<T>(CsvBinding binding, CsvReaderOptions<T> readerOptions)
        where T : unmanaged, IEquatable<T>;
}
