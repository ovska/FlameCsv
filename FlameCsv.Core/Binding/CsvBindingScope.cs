namespace FlameCsv.Binding;

/// <summary>
/// Scope on which the binding is valid.
/// </summary>
public enum CsvBindingScope
{
    /// <summary>The default, valid both when reading and writing CSV.</summary>
    All = 0,

    /// <summary>Valid only when reading CSV, e.g., on a constructor parameter.</summary>
    Read = 1,

    /// <summary>Valid only when writing CSV, e.g., on a read-only property.</summary>
    Write = 2,
}
