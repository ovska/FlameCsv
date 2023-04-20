namespace FlameCsv;

public enum CsvGetValueReason
{
    /// <summary>
    /// Value was successfully parsed.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The field was not found, index is out of range. If a header name was used,
    /// it's possible the current record has an invalid amount of fields.
    /// </summary>
    FieldNotFound = 1,

    /// <summary>
    /// Field with the specified name was not found.
    /// </summary>
    HeaderNotFound = 2,

    /// <summary>
    /// Field was found, but no parser was found for the specified type.
    /// </summary>
    NoParserFound = 3,

    /// <summary>
    /// The field could not be parsed into the specified type.
    /// </summary>
    UnparsableValue = 4,
}

