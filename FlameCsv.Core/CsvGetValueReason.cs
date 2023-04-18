namespace FlameCsv;

public enum CsvGetValueReason
{
    /// <summary>
    /// Value was successfully parsed.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The field was not found, either the index or the header name was invalid.
    /// </summary>
    FieldNotFound = 1,

    /// <summary>
    /// Field was found, but no parser was found for the specified type.
    /// </summary>
    NoParserFound = 2,

    /// <summary>
    /// The field could not be parsed into the specified type.
    /// </summary>
    UnparsableValue = 3,
}

