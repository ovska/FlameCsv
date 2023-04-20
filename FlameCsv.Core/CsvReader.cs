using System.IO.Pipelines;

namespace FlameCsv;

/// <summary>
/// Provides static methods for reading CSV records as objects or structs.
/// </summary>
public static partial class CsvReader
{
    /// <summary>
    /// Default buffer size used by the <see cref="CsvReader"/> when creating a <see cref="PipeReader"/>
    /// or a <see cref="TextReader"/>.
    /// </summary>
    public const int DefaultBufferSize = 4096;
}
