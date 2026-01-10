namespace FlameCsv;

/// <summary>
/// Enumeration that determines quote validation rules when reading.
/// </summary>
public enum CsvQuoteValidation : byte
{
    /// <summary>
    /// Quotes must be used strictly according to the CSV specification.
    /// Fields with invalid quotes will throw <see cref="Exceptions.CsvFormatException"/>.
    /// </summary>
    Strict = 0,

    /// <summary>
    /// Accept quotes even in fields that are not wrapped in quotes.<br/>
    /// Example: <c>foo"bar"</c>.
    /// </summary>
    /// <remarks>
    /// Quote parity is still required; every field must have an even number of quotes.
    /// Fields wrapped in quotes must still follow the quoting rules.
    /// </remarks>
    AllowInvalid = 1,

    /// <summary>
    /// Same as <see cref="Strict"/>, but validate all fields in every record even if they are not read.<br/>
    /// This affects the parsing and enumeration APIs. Has no effect while writing.
    /// </summary>
    /// <remarks>
    /// Enabling this has a performance impact, and should be enabled only when the data is expected to be malformed.
    /// </remarks>
    ValidateUnreadFields = 2,

    /// <summary>
    /// Same as <see cref="ValidateUnreadFields"/>, but also validate all fields of records skipped by
    /// <see cref="CsvOptions{T}.RecordCallback"/>.
    /// </summary>
    /// <remarks>
    /// Enabling this has a performance impact, and should be enabled only when the data is expected to be malformed.
    /// </remarks>
    ValidateAllRecords = 3,
}
