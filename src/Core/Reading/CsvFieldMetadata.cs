using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

/// <summary>
/// Metadata about a CSV field.
/// </summary>
public readonly struct CsvFieldMetadata
{
    private readonly uint _value;
    private readonly int _start;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvFieldMetadata"/>.
    /// </summary>
    /// <param name="value">Packed field end and flags</param>
    /// <param name="start">Start index in the data</param>
    [CLSCompliant(false)]
    public CsvFieldMetadata(uint value, int start)
    {
        _value = value;
        _start = start;
    }

    /// <summary>
    /// Length of the raw field, without unescaping and trimming.
    /// </summary>
    public int Length => (int)(_value & Field.EndMask) - _start;

    /// <summary>
    /// Returns <c>true</c> if the field has quotes; otherwise, <c>false</c>.
    /// </summary>
    public bool HasQuotes => Field.IsQuoted(_value);

    /// <summary>
    /// Returns <c>true</c> if the field needs unescaping; otherwise, <c>false</c>.
    /// </summary>
    /// <remarks>
    /// If this is <c>true</c>, <see cref="HasQuotes"/> is also <c>true</c>.
    /// </remarks>
    public bool NeedsUnescaping => (_value & Field.NeedsUnescapingMask) != 0;
}
