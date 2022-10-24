namespace FlameCsv;

[Flags]
public enum SecurityLevel
{
    /// <summary>
    /// CSV contents are not exposed outside the running code.
    /// </summary>
    Strict = 0,

    /// <summary>Rented buffers do not need to be cleared.</summary>
    NoBufferClearing = 1 << 0,

    /// <summary>Faulty/unparseable data can be included in exception messages.</summary>
    AllowExceptionMessages = 1 << 1,

    /// <summary>All flags set.</summary>
    Loose = ~0,
}
