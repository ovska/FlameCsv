namespace FlameCsv.Binding;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CsvTypeMapAttribute<TValue> : Attribute
{
    /// <summary>
    /// If <see langword="true"/>, headers that cannot be matched to a member are ignored.
    /// </summary>
    public bool IgnoreUnmatched { get; set; }

    /// <summary>
    /// If <see langword="true"/>, records that have unparsable values are ignored.
    /// </summary>
    public bool IgnoreUnparsable { get; set; }

    /// <summary>
    /// If <see langword="true"/>, duplicate matches from headers are ignored instead of throwing during runtime.
    /// </summary>
    public bool IgnoreDuplicate { get; set; }
}
